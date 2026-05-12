using System.Reflection;
using Barotrauma;
using HarmonyLib;
#if CLIENT
using Microsoft.Xna.Framework.Input;
#endif
using Networking;

namespace BarotraumaDieHard
{
    // 继承 ACsMod 
    public class Main : ACsMod
    {
        public static Harmony HarmonyInstance;

        // 1. 构造函数：LuaCs 加载 Mod 时会自动运行这里
        public Main()
        {
            HarmonyInstance = new Harmony("com.iri.diehard");
            HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
#if CLIENT
            GameMain.LuaCs.Hook.Add("think", "DieHardUpdate", (args) =>
            {
                // 1. 处理按键开关 UI
                /*if (PlayerInput.KeyHit(Keys.M))
                {
                    if (MonsterLootStore.monsterstorePaddedFrame == null)
                        MonsterLootStore.CreateTestStore();
                    else
                        MonsterLootStore.Close();
                }*/

                // 2. 核心：确保 UI 渲染
                MonsterLootStore.Update();
                
                return null;
            });
#endif
    #if SERVER
    // 服务器启动时注册网络监听
    NetUtil.Register(NetEvent.STORE_SELL, MonsterLootStore.OnReceiveSellItemMessage);
    NetUtil.Register(NetEvent.SWITCH_JUNCTIONBOX, PowerTransferPatch.OnReceiveJBSwitchMessage);
    #endif

            
            // 使用 Barotrauma 原生日志或 LuaCsLogger 都可以
            LuaCsLogger.Log("DieHard Mod Initialized via ACsMod Constructor.");
        }

        // 2. 必须重写 Stop 方法：这是 ACsMod 模式下的卸载点
        public override void Stop()
        {
            HarmonyInstance?.UnpatchAll("com.iri.diehard");
            HarmonyInstance = null;

            LuaCsLogger.Log("DieHard Mod Stopped and Unpatched.");
        }
    }
}