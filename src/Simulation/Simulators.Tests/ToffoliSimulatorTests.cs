﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Xunit;

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;

using Microsoft.Quantum.Simulation.Core;
using Microsoft.Quantum.Simulation.Simulators.Tests.Circuits;
using Microsoft.Quantum.Simulation.Simulators.Exceptions;

namespace Microsoft.Quantum.Simulation.Simulators.Tests
{
    public class ToffoliSimulatorTests
    {
        [Fact]
        public void ToffoliConstructor()
        {
            var subject = new ToffoliSimulator();

            Assert.Equal((int)ToffoliSimulator.DEFAULT_QUBIT_COUNT, subject.State.Length);
            Assert.Equal("Toffoli Simulator", subject.Name);
        }

        [Fact]
        public void ToffoliMeasure()
        {
            var sim = new ToffoliSimulator();
            var measure = sim.Get<Intrinsic.M>();

            var allocate = sim.Get<Intrinsic.Allocate>();
            var release = sim.Get<Intrinsic.Release>();

            var qubits = allocate.Apply(1);
            Assert.Single(qubits);

            var q = qubits[0];
            var result = measure.Apply(q);
            Assert.Equal(Result.Zero, result);

            sim.State[q.Id] = true;
            result = measure.Apply(q);
            Assert.Equal(Result.One, result);
            sim.State[q.Id] = false; // Make it safe to release

            release.Apply(qubits);
            sim.CheckNoQubitLeak();
        }

        [Fact]
        public void ToffoliX()
        {
            var sim = new ToffoliSimulator();
            var x = sim.Get<Intrinsic.X>();

            OperationsTestHelper.applyTestShell(sim, x, (q) =>
            {
                var measure = sim.Get<Intrinsic.M>();
                var set = sim.Get<SetQubit>();

                set.Apply((Result.Zero, q));

                x.Apply(q);
                var result = measure.Apply(q);
                Assert.Equal(Result.One, result);

                x.Apply(q);
                result = measure.Apply(q);
                Assert.Equal(Result.Zero, result);

                x.Apply(q); // The test helper is going to apply another X before releasing
            });
        }

        [Fact]
        public void ToffoliXAdjoint()
        {
            var sim = new ToffoliSimulator();
            var x = sim.Get<Intrinsic.X>() as IUnitary<Qubit>;

            OperationsTestHelper.applyTestShell(sim, x.Adjoint, (q) =>
            {
                var measure = sim.Get<Intrinsic.M>();
                var set = sim.Get<SetQubit>();

                set.Apply((Result.One, q));

                x.Apply(q);
                x.Adjoint.Apply(q);
                var result = measure.Apply(q);
                Assert.Equal(Result.One, result);
            });
        }

        [Fact]
        public void ToffoliXCtrl()
        {
            var sim = new ToffoliSimulator();

            var x = sim.Get<Intrinsic.X>();
            var measure = sim.Get<Intrinsic.M>();
            var set = sim.Get<SetQubit>();

            var ctrlX = x.ControlledBody.AsAction();

            OperationsTestHelper.ctrlTestShell(sim, ctrlX, (enabled, ctrls, q) =>
            {
                set.Apply((Result.Zero, q));
                x.ControlledBody((ctrls, q));

                var result = measure.Apply(q);
                var expected = (enabled) ? Result.One : Result.Zero;
                Assert.Equal(expected, result);

                // Clean up 
                foreach (var c in ctrls)
                {
                    set.Apply((Result.Zero, c));
                }
                // The test driver applies a controlled-X after this returns,
                // so if ctrls is empty we need to reset to One instead of 0.
                set.Apply((ctrls.Length > 0 ? Result.Zero : Result.One, q));
            });
        }

        [Fact]
        public async Task ToffoliSwap()
        {
            var sim = new ToffoliSimulator();

            await Circuits.SwapTest.Run(sim);
        }

        [Fact]
        public async Task Bug2469()
        {
            var sim = new ToffoliSimulator();

            await Circuits.TestSafeToRunCliffords.Run(sim, false);
        }

        [Fact]
        public async Task ToffoliUsingCheck()
        {
            var sim = new ToffoliSimulator();

            await Assert.ThrowsAsync<ReleasedQubitsAreNotInZeroState>(() => ToffoliUsingQubitCheck.Run(sim));
        }

        [Fact]
        public async Task ToffoliBorrowingCheck()
        {
            var sim = new ToffoliSimulator();

            await Assert.ThrowsAsync<ExecutionFailException>(() => ToffoliBorrowingQubitCheck.Run(sim));
        }

        [Fact]
        public void ToffoliFunctors()
        {
            var sim = new ToffoliSimulator();
            var measure = sim.Get<Intrinsic.M>();
            var allocate = sim.Get<Intrinsic.Allocate>();
            var release = sim.Get<Intrinsic.Release>();
            var setQubit = sim.Get<SetQubit>() as ICallable<(Result, Qubit), QVoid>;
            var x = sim.Get<Intrinsic.X>() as IUnitary<Qubit>;

            var qubits = allocate.Apply(3);
            var ctrls = new QArray<Qubit> (qubits[0]);
            var ctrls2 = new QArray<Qubit> (qubits[1]);
            var q1 = qubits[2];

            var r = measure.Apply(q1);
            Assert.Equal(Result.Zero, r);

            /// This shows what PartialApp codegen looks like for:
            ///     let setOne = SetQubit(One, _)
            var setOne = setQubit.Partial((Result.One, AbstractCallable._));
            setOne.Apply(q1);

            r = measure.Apply(q1);
            Assert.Equal(Result.One, r);

            x.Adjoint.Adjoint.Apply(q1);
            r = measure.Apply(q1);
            Assert.Equal(Result.Zero, r);

            x.Adjoint.Apply(q1);
            r = measure.Apply(q1);
            Assert.Equal(Result.One, r);

            x.Adjoint.Controlled.Apply((ctrls, q1));
            r = measure.Apply(q1);
            Assert.Equal(Result.One, r);

            x.Controlled.Adjoint.Apply((ctrls, q1));
            r = measure.Apply(q1);
            Assert.Equal(Result.One, r);

            x.Controlled.Controlled.Apply((ctrls, (ctrls2, q1)));
            r = measure.Apply(q1);
            Assert.Equal(Result.One, r);

            // Reset to Z=0 before releasing
            x.Apply(q1);

            release.Apply(qubits);
        }

        [Fact]
        public void ToffoliDumpState()
        {
            var sim = new ToffoliSimulator();

            var allocate = sim.Get<Intrinsic.Allocate>();
            var release = sim.Get<Intrinsic.Release>();
            var dumpMachine = sim.Get<Microsoft.Quantum.Diagnostics.DumpMachine>();

            var x = sim.Get<Intrinsic.X>();

            // We define a local function to prepare an example state so that we
            // can repeat it at the end to release everything.
            void Prepare(IEnumerable<Qubit> qubits)
            {
                foreach (var qubit in qubits.EveryNth(3))
                {
                    x.Apply(qubit);
                }
            };

            // We'll need to override console output writers, so we start by
            // recording the current stream used as stdout.
            var stdout = Console.Out;

            try
            {
                // Start with a small example (< 32 qubits).
                var qubits = allocate.Apply(13);
                Prepare(qubits);

                // Dump the state out.
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);

                    sim.DumpFormat = ToffoliDumpFormat.Automatic;
                    dumpMachine.Apply("");
                    var expected = @"Offset  \tState Data
========\t==========
00000000\t1001001001001
"
                        .Replace("\\t", "\t");
                    Assert.Equal(writer.ToString());
                    writer.Clear();

                    sim.DumpFormat = ToffoliDumpFormat.Bits;
                    dumpMachine.Apply("");
                    var expected = @"Offset  \tState Data
========\t==========
00000000\t1001001001001
"
                        .Replace("\\t", "\t");
                    Assert.Equal(writer.ToString());
                    writer.Clear();

                    sim.DumpFormat = ToffoliDumpFormat.Hex;
                    dumpMachine.Apply("");
                    var expected = @"Offset  \tState Data
========\t==========
00000000\t1249
"
                        .Replace("\\t", "\t");
                    Assert.Equal(writer.ToString());
                    writer.Clear();
                }

                // Reset and return our qubits for the next example.
                Prepare(qubits);
                release.Apply(qubits);

                // Proceed with a larger example (≥ 32 qubits).
                var qubits = allocate.Apply(64);
                Prepare(qubits);

                // Dump the state out.
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);

                    sim.DumpFormat = ToffoliDumpFormat.Automatic;
                    dumpMachine.Apply("");
                    var expected = @"Offset  \tState Data
========\t==========
00000000\t9249249249249249
"
                        .Replace("\\t", "\t");
                    Assert.Equal(writer.ToString());
                    writer.Clear();

                    sim.DumpFormat = ToffoliDumpFormat.Bits;
                    dumpMachine.Apply("");
                    var expected = @"Offset  \tState Data
========\t==========
00000000\t1001001001001001
00000002\t0010010010010010
00000004\t0100100100100100
00000006\t1001001001001001
"
                        .Replace("\\t", "\t");
                    Assert.Equal(writer.ToString());
                    writer.Clear();

                    sim.DumpFormat = ToffoliDumpFormat.Hex;
                    dumpMachine.Apply("");
                    var expected = @"Offset  \tState Data
========\t==========
00000000\t9249249249249249
"
                        .Replace("\\t", "\t");
                    Assert.Equal(writer.ToString());
                    writer.Clear();
                }

                // Reset and return our qubits for the next example.
                Prepare(qubits);
                release.Apply(qubits);
            }
            finally
            {
                // Restore stdout on our way out.
                Console.SetOut(stdout);
            }
        }

    }

    internal static class TestUtilityExtensions
    {
        internal static IEnumerable<T> EveryNth<T>(this IEnumerable<T> source, int n = 2) =>
            source.Where((element, index) => index % n == 0);

        internal static void Clear(this StringWriter writer)
        {
            var builder = writer.GetStringBuilder();
            builder.Remove(0, builder.Length);
        }
    }
}
