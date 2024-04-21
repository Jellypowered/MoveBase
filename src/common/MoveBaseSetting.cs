using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace MoveBase
{
    public class MoveBaseSetting : ModSettings
    {
        //public List<FeatureNews> FeatureNews = new List<FeatureNews>();

        public MoveBaseSetting()
        {
            //FeatureNews news = new FeatureNews(
            //            "Home Mover update"
            //            , $"Now, Home Mover allows you to move buildings to positions that are occupied, in a different formation, by the same buildings."
            //            , @"https://steamcommunity.com/sharedfiles/filedetails/?id=2092552843"
            //            , "Check out the demo"
            //            , new DateTime(2020, 6, 15));

            //FeatureNews.Add(news);
        }

        public override void ExposeData()
        {
            //base.ExposeData();
            //Scribe_Collections.Look(ref FeatureNews, nameof(FeatureNews), LookMode.Deep);
        }
    }
}
