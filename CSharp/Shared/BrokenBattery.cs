using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Items.Components;
using Barotrauma.Networking;
using Barotrauma.Extensions;
using Barotrauma;


namespace BarotraumaDieHard
{
    class BrokenBattery : ItemComponent
    {
		private float brokenbatteryUpdateTimer;
        private const float BrokenbatteryUpdateInterval = 1.0f;

        public BrokenBattery(Item item, ContentXElement element)
            : base(item, element)
        {
            brokenbatteryUpdateTimer = Rand.Range(0.0f, BrokenbatteryUpdateInterval);
            IsActive = true;
        }
		
		 
		
        public override void Update(float deltaTime, Camera cam)
        {
            if (item == null || item.Removed || item.CurrentHull == null) return;
            if (!item.InPlayerSubmarine) return;
           
                    
                    if(item.Condition < 20f)
                    {
                        
                        HullMod.AddGas(item.CurrentHull, "Chlorine", 30f, deltaTime);
                        
                    }
        }
            

        public override void UpdateBroken(float deltaTime, Camera cam)
        {
            base.UpdateBroken(deltaTime, cam);
            if (!item.InPlayerSubmarine || item == null || item.CurrentHull == null) {return;}
               
                brokenbatteryUpdateTimer -= deltaTime;
                
                if (brokenbatteryUpdateTimer < 0.0f)
                {
                    HullMod.AddGas(item.CurrentHull, "Chlorine", 10f, deltaTime);
                    brokenbatteryUpdateTimer = BrokenbatteryUpdateInterval;
                }
        }

        
    }
}
