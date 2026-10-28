using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Agora.Tests
{
    /// <summary>Κοινοί helpers των verification tests. Φορτώνει τα assets μία φορά.</summary>
    public static class TestWorld
    {
        static GameContent content;

        public static GameContent Content
        {
            get
            {
                if (content != null) return content;
                string guid = AssetDatabase.FindAssets("t:GameContent").FirstOrDefault();
                Assert.IsNotNull(guid, "Δεν βρέθηκε GameContent asset — τρέξε Generate Content + Import Texts.");
                content = AssetDatabase.LoadAssetAtPath<GameContent>(AssetDatabase.GUIDToAssetPath(guid));
                return content;
            }
        }

        /// <summary>Config με τα defaults του κώδικα (spec ενότητα Γ).</summary>
        public static GameConfig Cfg(float noise = -1f)
        {
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            if (noise >= 0f) cfg.noise = noise;
            return cfg;
        }

        public static CaseData Case(int id)
        {
            var c = Content.cases.FirstOrDefault(x => x != null && x.id == id);
            Assert.IsNotNull(c, "Λείπει η υπόθεση " + id);
            return c;
        }

        public static CityState City(int t, int ep, int et, int em, int id, bool war = false)
        {
            var city = new CityState(50) { treasury = t, food = ep, readiness = et, trust = em, demosPower = id, war = war };
            return city;
        }

        public static PlayerState Player(GameConfig cfg, SocialClass cls = SocialClass.Zeugites) => new PlayerState(cfg, cls, "Test");

        public static OfficialData Official(string key)
        {
            string path = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("Official_" + key + " t:OfficialData").FirstOrDefault());
            var official = AssetDatabase.LoadAssetAtPath<OfficialData>(path);
            Assert.IsNotNull(official, "Λείπει το Official_" + key);
            return official;
        }

        public static Metric ParseMetric(string key) => key switch
        {
            "T" => Metric.Treasury,
            "Ep" => Metric.Food,
            "Et" => Metric.Readiness,
            "Em" => Metric.Trust,
            "ID" => Metric.DemosPower,
            _ => throw new ArgumentException(key)
        };
    }
}
