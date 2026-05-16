using Barotrauma.Networking;
using FarseerPhysics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Barotrauma.Extensions;
using FarseerPhysics.Dynamics;
using System.Collections.Immutable;
using Barotrauma.Items.Components;

using Barotrauma;
using Networking;

#if CLIENT
using Microsoft.Xna.Framework.Graphics;
#endif

namespace BarotraumaDieHard
{
    class TorpedoTurret : Turret
    {


#if CLIENT
		private GUIFrame mainFrame;
		private GUIButton launchButton;
		private GUITextBlock launchButtonText;
#endif
        
        public override void OnItemLoaded()
        {
            base.OnItemLoaded();

#if CLIENT
            if (GuiFrame == null) { return; }
            if (mainFrame == null)
            {
                mainFrame = new GUIFrame(new RectTransform(new Vector2(0.9f, 0.9f), GuiFrame.RectTransform, Anchor.Center), null);
            }

            var launchButtonArea = new GUIFrame(new RectTransform(new Vector2(0.25f, 0.5f), mainFrame.RectTransform, Anchor.CenterLeft)
            {
                RelativeOffset = new Vector2(0, 0.07f)
            }, style: null);

			launchButtonText = new GUITextBlock(new RectTransform(new Vector2(2f, 1f), launchButtonArea.RectTransform, Anchor.CenterRight)
			{
                RelativeOffset = new Vector2(-2.5f, 0.0f)
            }, TextManager.Get("LaunchTorpedo"), textAlignment: Alignment.Center, font: GUIStyle.SubHeadingFont);

            launchButton = new GUIButton(new RectTransform(new Vector2(1f, 1f), launchButtonArea.RectTransform, Anchor.Center)
            {
                RelativeOffset = new Vector2(0, 0.1f)
            }, style: "PowerButton")
            {
                //UserData = UIHighlightAction.ElementId.PowerButton,
                OnClicked = (button, data) =>
                {
                    //TargetLevel = null;
                    //IsActive = !IsActive;
					//DebugConsole.NewMessage("TryLaunchTorpedo");
					TryLaunchTorpedo(0.0f, Character.Controlled, true);
                    if (GameMain.Client != null)
                    {
                        correctionTimer = CorrectionDelay;
						
                        SendTryLaunchTorpedoMessage(item);
						
                    }
                    
                    return true;
                }
            };
#endif

        }
        
        public TorpedoTurret(Item item, ContentXElement element)
            : base(item, element)
        {
		}

		private bool TryLaunchTorpedo(float deltaTime, Character character = null, bool ignorePower = false)
        {
			//DebugConsole.NewMessage("TryLaunch");
            tryingToCharge = true;
            if (GameMain.NetworkMember != null && GameMain.NetworkMember.IsClient) { return false; }

            if (currentChargeTime < MaxChargeTime) { return false; }

            if (reload > 0.0f) { return false; }

            if (MaxActiveProjectiles >= 0)
            {
                activeProjectiles.RemoveAll(it => it.Removed);
                if (activeProjectiles.Count >= MaxActiveProjectiles)
                {
                    return false;
                }
            }
            
            if (!ignorePower)
            {
                if (!HasPowerToShoot())
                {
#if CLIENT
                    if (!flashLowPower && character != null && character == Character.Controlled)
                    {
                        flashLowPower = true;
                        SoundPlayer.PlayUISound(GUISoundType.PickItemFail);
                    }
#endif
                    return false;
                }
            }

            Projectile launchedProjectile = null;
            bool loaderBroken = false;
            float tinkeringStrength = 0f;

            for (int i = 0; i < ProjectileCount; i++)
            {
                var projectiles = GetLoadedProjectiles();
                if (projectiles.Any())
                {
                    ItemContainer projectileContainer = projectiles.First().Item.Container?.GetComponent<ItemContainer>();
                    if (projectileContainer != null && projectileContainer.Item != item)
                    {
                        //user needs to be null because the ammo boxes shouldn't be directly usable by characters
                        projectileContainer?.Item.Use(deltaTime, user: null, userForOnUsedEvent: user);
                    }
                }
                else
                {
                    for (int j = 0; j < item.linkedTo.Count; j++)
                    {
                        var e = item.linkedTo[(j + currentLoaderIndex) % item.linkedTo.Count];
                        //use linked projectile containers in case they have to react to the turret being launched somehow
                        //(play a sound, spawn more projectiles)
                        if (e is not Item linkedItem) { continue; }
                        if (!item.Prefab.IsLinkAllowed(e.Prefab)) { continue; }
                        if (linkedItem.Condition <= 0.0f)
                        {
                            loaderBroken = true;
                            continue;
                        }
                        if (tryUseProjectileContainer(linkedItem)) { break; }
                    }
                    tryUseProjectileContainer(item);

                    bool tryUseProjectileContainer(Item containerItem)
                    {
                        ItemContainer projectileContainer = containerItem.GetComponent<ItemContainer>();
                        if (projectileContainer != null)
                        {
                            containerItem.Use(deltaTime, user: null, userForOnUsedEvent: user);
                            projectiles = GetLoadedProjectiles();
                            if (projectiles.Any()) { return true; }                            
                        }
                        return false;
                    }
                }
                if (projectiles.Count == 0 && !LaunchWithoutProjectile)
                {
                    //coilguns spawns ammo in the ammo boxes with the OnUse statuseffect when the turret is launched,
                    //causing a one frame delay before the gun can be launched (or more in multiplayer where there may be a longer delay)
                    //  -> attempt to launch the gun multiple times before showing the "no ammo" flash
                    failedLaunchAttempts++;
#if CLIENT
                    if (!flashNoAmmo && !flashLoaderBroken && character != null && character == Character.Controlled && failedLaunchAttempts > 20)
                    {
                        if (loaderBroken)
                        {
                            flashLoaderBroken = true;
                        }
                        else
                        {
                            flashNoAmmo = true;
                        }
                        failedLaunchAttempts = 0;
                        SoundPlayer.PlayUISound(GUISoundType.PickItemFail);
                    }
#endif
                    return false;
                }
                failedLaunchAttempts = 0;

                foreach (MapEntity e in item.linkedTo)
                {
                    if (e is not Item linkedItem) { continue; }
                    if (!((MapEntity)item).Prefab.IsLinkAllowed(e.Prefab)) { continue; }
                    if (linkedItem.GetComponent<Repairable>() is Repairable repairable && repairable.IsTinkering && linkedItem.HasTag(Tags.TurretAmmoSource))
                    {
                        tinkeringStrength = repairable.TinkeringStrength;
                    }
                }

                if (!ignorePower)
                {
                    var batteries = GetDirectlyConnectedBatteries().Where(static b => !b.OutputDisabled && b.Charge > 0.0001f && b.MaxOutPut > 0.0001f);
                    float neededPower = GetPowerRequiredToShoot();
                    // tinkering is currently not factored into the common method as it is checked only when shooting
                    // but this is a minor issue that causes mostly cosmetic woes. might still be worth refactoring later
                    neededPower /= 1f + (tinkeringStrength * TinkeringPowerCostReduction);
                    while (neededPower > 0.0001f && batteries.Any())
                    {
                        float takePower = neededPower / batteries.Count();
                        takePower = Math.Min(takePower, batteries.Min(b => Math.Min(b.Charge * 3600.0f, b.MaxOutPut)));
                        foreach (PowerContainer battery in batteries)
                        {
                            neededPower -= takePower;
                            battery.Charge -= takePower / 3600.0f;
#if SERVER
                            battery.Item.CreateServerEvent(battery);                        
#endif
                        }
                    }
                }

                launchedProjectile = projectiles.FirstOrDefault();
                Item container = launchedProjectile?.Item.Container;
                if (container != null)
                {
                    var repairable = launchedProjectile?.Item.Container.GetComponent<Repairable>();
                    if (repairable != null)
                    {
                        repairable.LastActiveTime = (float)Timing.TotalTime + 1.0f;
                    }
                }

                if (launchedProjectile != null || LaunchWithoutProjectile)
                {
                    if (projectiles.Any())
                    {
                        foreach (Projectile projectile in projectiles)
                        {
                            Launch(projectile.Item, character, tinkeringStrength: tinkeringStrength);
                        }
                    }
                    else
                    {
                        Launch(null, character, tinkeringStrength: tinkeringStrength);
                    }
                    if (item.AiTarget != null)
                    {
                        item.AiTarget.SoundRange = item.AiTarget.MaxSoundRange;
                        // Turrets also have a light component, which handles the sight range.
                    }
                    if (container != null)
                    {
                        ShiftItemsInProjectileContainer(container.GetComponent<ItemContainer>());
                    }
                    if (item.linkedTo.Count > 0)
                    {
                        currentLoaderIndex = (currentLoaderIndex + 1) % item.linkedTo.Count;
                    }
                }
            }

#if SERVER
            if (character != null && launchedProjectile != null)
            {
                string msg = GameServer.CharacterLogName(character) + " launched " + item.Name + " (projectile: " + launchedProjectile.Item.Name;
                var containedItems = launchedProjectile.Item.ContainedItems;
                if (containedItems == null || !containedItems.Any())
                {
                    msg += ")";
                }
                else
                {
                    msg += ", contained items: " + string.Join(", ", containedItems.Select(i => i.Name)) + ")";
                }
                GameServer.Log(msg, ServerLog.MessageType.ItemInteraction);
            }
#endif

            return true;
        }


#if CLIENT
    private void SendTryLaunchTorpedoMessage(Item item)
    {
        IWriteMessage msg = NetUtil.CreateNetMsg(NetEvent.TORPEDOTUBE_TRYLAUNCH);

        msg.WriteUInt16(item.ID); // ID of the torpedo turret
        NetUtil.SendServer(msg, DeliveryMethod.Reliable);
    }
#endif
	public static void OnReceiveTryLaunchTorpedoMessage(object[] args)
    {
        IReadMessage msg = (IReadMessage)args[0];
        ushort itemId = msg.ReadUInt16();

        // Find the torpedo turret item
        Item turretItem = Entity.FindEntityByID(itemId) as Item;
        if (turretItem != null)
        {
            var torpedoTurret = turretItem.GetComponent<TorpedoTurret>();
            if (torpedoTurret != null)
            {
                // Call the TryLaunchTorpedo method on the server side
                torpedoTurret.TryLaunchTorpedo(0.0f, null, true);
            }
        }
    }

	}
}
