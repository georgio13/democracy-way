using System;
namespace DemocracyWay.NewUI
{
    [Serializable] public sealed class CityMetricDelta
    {
        public int food, trust, readiness, war;
        public CityMetricDelta Copy() => new CityMetricDelta { food=food,trust=trust,readiness=readiness,war=war };
    }
}
