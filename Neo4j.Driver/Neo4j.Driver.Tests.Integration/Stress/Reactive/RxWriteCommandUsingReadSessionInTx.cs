// Copyright (c) "Neo4j"
// Neo4j Sweden AB [http://neo4j.com]
// 
// This file is part of Neo4j.
// 
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Reactive.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Neo4j.Driver.Internal;
using Xunit;

namespace Neo4j.Driver.IntegrationTests.Stress;

public class RxWriteCommandUsingReadSessionInTx : RxCommand
{
    public RxWriteCommandUsingReadSessionInTx(IDriver driver, bool useBookmark)
        : base(driver, useBookmark)
    {
    }

    public override async Task ExecuteAsync(StressTestContext context)
    {
        var session = NewSession(AccessMode.Read, context);
        var result = default(IRxResult);

        var exc = await Record.ExceptionAsync(
            async () => await BeginTransaction(session, context)
                .SelectMany(
                    txc =>
                    {
                        result = txc.Run("CREATE ()");

                        return result
                            .Records()
                            .CatchAndThrow(_ => txc.Rollback<IRecord>())
                            .Concat(txc.Commit<IRecord>());
                    })
                .CatchAndThrow(_ => session.Close<IRecord>())
                .Concat(session.Close<IRecord>()));

        exc.Should().BeOfType<ClientException>();

        result.Should().NotBeNull();
        var summary = await result.Consume();
        summary.Counters.NodesCreated.Should().Be(0);
    }
}
