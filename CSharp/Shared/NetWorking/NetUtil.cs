﻿
using Barotrauma;
using Barotrauma.Networking;
using System;

namespace Networking
{
    /// <summary>
    /// Shared
    /// </summary>
    public static partial class NetUtil
    {
        internal static IWriteMessage CreateNetMsg(NetEvent target) => GameMain.LuaCs.Networking.Start(Enum.GetName(typeof(NetEvent), target));

        /// <summary>
        /// Register a method to run when the specified NetEvent happens
        /// </summary>
        /// <param name="target"></param>
        /// <param name="netEvent"></param>
        public static void Register(NetEvent target, LuaCsAction netEvent)
        {
            if (GameMain.IsSingleplayer) return;
            GameMain.LuaCs.Networking.Receive(Enum.GetName(typeof(NetEvent), target), netEvent);
        }
    }

    /// <summary>
    /// Events that are sent over the network
    /// </summary>
    public enum NetEvent
    {
        TORPEDOTUBE_ARM,
        CUSTOM_OXYGENGENERATOR_TOGGLE,
        CUSTOM_OXYGENGENERATOR_GENERATEDAMOUNTFACTOR,
        TORPEDOTUBE_TRYLAUNCH,
        APPLY_SONAR_PING_DAMAGE,
        VERTICAL_ENGINE_POWER_CHANGE,
        SWITCH_JUNCTIONBOX,
        DOOR_JAMMED_STATE_CHANGE,
        MISSION_START,
        SONAR_CHANGERANGE,
        CUSTOM_OXYGENGENERATOR_REFILLTOGGLE,
        VECTORED_ENGINE_FORCECHANGE,
        VECTORED_ENGINE_MAINTAINDEPTH,
        STORE_SELL
    }
}