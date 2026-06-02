using System.Reflection;
using Barotrauma;
using HarmonyLib;
#if CLIENT
using Microsoft.Xna.Framework.Input;
#endif
using Networking;
using System.Diagnostics;
using System.Collections.Generic;

namespace BarotraumaDieHard
{
    // 继承 ACsMod 
    public partial class Main : ACsMod
    {
        public static Harmony HarmonyInstance;

        // 1. 构造函数：LuaCs 加载 Mod 时会自动运行这里
        public Main()
        {
            HarmonyInstance = new Harmony("com.iri.diehard");
            HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());

            // 调用部分方法（如果客户端文件存在，就会执行那边的逻辑）
            InitClient();
            ModProfiler.AutoHookAllUpdates(HarmonyInstance);
            //WreckMethodTracker.HookAllSubmarineMethods(HarmonyInstance);

#if SERVER
    // 服务器启动时注册网络监听。只写在server里
    
#endif

        //有趣。以前都是只在服务器注册的，shared注册会崩溃。现在看来不是这样。
        //必须都注册才能让bot正常从服务器端发送到客户端的指令
            if (!GameMain.IsSingleplayer)
            {
                //--junctionbox
                NetUtil.Register(NetEvent.SWITCH_JUNCTIONBOX, PowerTransferPatch.OnReceiveJBSwitchMessage);
                //--wire
                NetUtil.Register(NetEvent.WIRE_DISCONNECT_SYNC, AIObjectiveRepairWithDisconnect.OnReceiveWireSync);

                //--oxygengenerator
                NetUtil.Register(NetEvent.CUSTOM_OXYGENGENERATOR_GENERATEDAMOUNTFACTOR, CustomOxygenGenerator.OnReceiveGeneratedAmountFactorMessage);
                NetUtil.Register(NetEvent.CUSTOM_OXYGENGENERATOR_REFILLTOGGLE, CustomOxygenGenerator.OnReceiveToggleChargingOxygenTankMessage);
                NetUtil.Register(NetEvent.CUSTOM_OXYGENGENERATOR_TOGGLE, CustomOxygenGenerator.OnReceiveToggleOxygenGeneratorMessage);
                //--lock door
                NetUtil.Register(NetEvent.DOOR_JAMMED_STATE_CHANGE, MiniMapLegacy.OnReceiveDoorJamMessage);

                //--engine
                NetUtil.Register(NetEvent.VECTORED_ENGINE_FORCECHANGE, CustomEngine.OnReceiveVerticalForceMessage);
                NetUtil.Register(NetEvent.VECTORED_ENGINE_MAINTAINDEPTH, CustomEngine.OnReceiveMaintainDepthMessage);

                //--Sonar
                NetUtil.Register(NetEvent.APPLY_SONAR_PING_DAMAGE, SonarMod.OnReceiveSonarPingApplyDamageMessage);
                NetUtil.Register(NetEvent.SONAR_CHANGERANGE, SonarMod.OnReceiveChangeRangeMessage);

                //-- TorpedoTube
                NetUtil.Register(NetEvent.TORPEDOTUBE_ARM, TorpedoTube.OnReceiveArmTorpedoMessage);

                //-- TorpedoTurret
                NetUtil.Register(NetEvent.TORPEDOTUBE_TRYLAUNCH, TorpedoTurret.OnReceiveTryLaunchTorpedoMessage);

                NetUtil.Register(NetEvent.STORE_SELL, MonsterLootStore.OnReceiveSellItemMessage);

                NetUtil.Register(NetEvent.SYNC_DOOR_HALF_OPEN, DoorPatch.OnReceiveDoorHalfOpenMessage);
                NetUtil.Register(NetEvent.CONTAINER_LOCK_STATE, KeyLock.OnReceiveContainerLockStateMessage);
                NetUtil.Register(NetEvent.DESTROY_MAIN_WALL, ExplosionDamageWallPatch.OnReceiveDestroyWallMessage);

            }

            
            // 使用 Barotrauma 原生日志或 LuaCsLogger 都可以
            //LuaCsLogger.Log("DieHard Mod Initialized via ACsMod Constructor.");
        }

        // 声明一个部分方法
        partial void InitClient();

        // 2. 必须重写 Stop 方法：这是 ACsMod 模式下的卸载点
        public override void Stop()
        {
            HarmonyInstance?.UnpatchAll("com.iri.diehard");
            HarmonyInstance = null;

            LuaCsLogger.Log("DieHard Mod Stopped and Unpatched.");
        }
    }
}