﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.
using SiliconStudio.Paradox.Games;
using SiliconStudio.Paradox.Physics;

namespace SiliconStudio.Paradox.Engine
{
    public interface IPhysicsSystem : IGameSystemBase
    {
        PhysicsEngine PhysicsEngine { get; }
    }
}