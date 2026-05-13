using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Barotrauma.Extensions;
using Barotrauma;

namespace ButVentMod//todo make a structural namespace DieHard.Item.Components. namespace can't be used in elsewhere
{
    class BugVent : ItemComponent
    {
		private float bugAppearChance;
		private bool onlyPlayerSub;
		private float updateInterval;
		private float elapsedTime;
		private float bugventUpdateTimer;
		private float ventUpdateTimer;
		
        /*public override void OnItemLoaded()
        {
            base.OnItemLoaded();
        }*/

        public BugVent(Item item, ContentXElement element)
            : base(item, element)
        {
            ventUpdateTimer = Rand.Range(updateInterval - 30, updateInterval + 30);
            IsActive = true;
        }
		
		[Editable, Serialize(0.01f, IsPropertySaveable.Yes, description: "The change that a bug can appear from a vent.", alwaysUseInstanceValues: true)]
        public float BugAppearChance
        {
            get { return bugAppearChance; }
            set { bugAppearChance = MathHelper.Clamp(value, 0f, 1f); }
        }
		
		[Editable, Serialize(true, IsPropertySaveable.Yes, description: "Does it only apply to player's sub?", alwaysUseInstanceValues: true)]
        public bool OnlyPlayerSub
        {
            get { return onlyPlayerSub; }
            set { onlyPlayerSub = value; }
        }
		[Editable, Serialize(300f, IsPropertySaveable.Yes, description: "How frequent the update takes place. The interval will be slightly higher or lower to prevent simultaneous update for all vents.", alwaysUseInstanceValues: true)]
        public float UpdateInterval
        {
            get { return updateInterval; }
			set { updateInterval = MathHelper.Clamp(value, 60f, 600f); }
            
        }
		
        public override void Update(float deltaTime, Camera cam)
        {
            
            if (!item.InPlayerSubmarine && onlyPlayerSub) {return;}

           // 如果当前关卡是 Outpost，则不执行
            if (GameMain.GameSession?.Level?.LevelData?.Type == LevelData.LevelType.Outpost) { return; }
            
            // Increment the elapsed time
            elapsedTime += deltaTime;

            // Check if it's time to perform the action
            if (elapsedTime >= ventUpdateTimer)
            {
                // Reset the elapsed time
                elapsedTime = 0.0f;
                

                // Perform the action with a chance
                if (Rand.Range(0.0f, 1.0f) < bugAppearChance)
                {
#if CLIENT
                    BarotraumaDieHard.CustomHintManager.DisplayHint("bugvent".ToIdentifier());
#endif
                    
					Entity.Spawner.AddCharacterToSpawnQueue("Electrical_bug",item.WorldPosition);
                }
            }
            
        }

        
    }
}
