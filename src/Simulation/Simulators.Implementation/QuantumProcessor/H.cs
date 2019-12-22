﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Quantum.Simulation.Core;

namespace Microsoft.Quantum.Simulation.QuantumProcessor
{
    public partial class QuantumProcessorDispatcher
    {
        public class QuantumProcessorDispatcherH : Quantum.Intrinsic.H
        {
            private QuantumProcessorDispatcher Simulator { get; }

            public QuantumProcessorDispatcherH(QuantumProcessorDispatcher m) : base(m)
            {
                this.Simulator = m;
            }

            public override Func<Qubit, QVoid> Body => (q1) =>
            {
                Simulator.QuantumProcessor.H(q1);
                return QVoid.Instance;
            };


            public override Func<(IQArray<Qubit>, Qubit), QVoid> ControlledBody => (args) =>
            {
                var (ctrls, q1) = args;
                Simulator.QuantumProcessor.ControlledH(ctrls, q1);
                return QVoid.Instance;
            };
        }
    }
}