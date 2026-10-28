using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    internal sealed class GwpCityStreetBody
    {
        internal string Character = string.Empty, Equipment = string.Empty, Body = string.Empty, Action = string.Empty, ActionSet = string.Empty;
        internal bool Killed, LocationActor;
        internal int Seed;
        internal uint Color1, Color2;
        internal Vec3 Position;
        internal Vec2 Direction;
    }

    internal sealed partial class GwpCitySearchBehavior
    {
        // 同版存读只保存值，不把旧Mission里的Agent/GameEntity引用带到下一场景。
        private List<string> _streetBodyData = new();
        private List<double> _streetBodyNumbers = new();
        private const int BodyStringStride = 5;
        private const int BodyNumberStride = 9;

        private void SyncStreetBodies(IDataStore store)
        {
            store.SyncData("gwp_cs_street_body_data", ref _streetBodyData);
            store.SyncData("gwp_cs_street_body_numbers", ref _streetBodyNumbers);
            _streetBodyData ??= new();
            _streetBodyNumbers ??= new();
        }

        private void ClearStreetBodies()
        {
            _streetBodyData.Clear();
            _streetBodyNumbers.Clear();
        }

        internal void SaveStreetBodies(IEnumerable<GwpCityStreetBody> bodies)
        {
            ClearStreetBodies();
            foreach (GwpCityStreetBody body in bodies)
            {
                _streetBodyData.AddRange(new[] { body.Character, body.Equipment, body.Body, body.Action, body.ActionSet });
                _streetBodyNumbers.AddRange(new double[] { (body.Killed ? 1 : 0) | (body.LocationActor ? 2 : 0),
                    body.Seed, body.Color1, body.Color2, body.Position.x, body.Position.y, body.Position.z,
                    body.Direction.x, body.Direction.y });
            }
        }

        internal IEnumerable<GwpCityStreetBody> StreetBodies
        {
            get
            {
                for (int i = 0; i < _streetBodyData.Count / BodyStringStride; i++)
                {
                    int text = i * BodyStringStride, number = i * BodyNumberStride;
                    int flags = (int)_streetBodyNumbers[number];
                    yield return new GwpCityStreetBody
                    {
                        Character = _streetBodyData[text], Equipment = _streetBodyData[text + 1],
                        Body = _streetBodyData[text + 2], Action = _streetBodyData[text + 3], ActionSet = _streetBodyData[text + 4],
                        Killed = (flags & 1) != 0, LocationActor = (flags & 2) != 0,
                        Seed = (int)_streetBodyNumbers[number + 1], Color1 = (uint)_streetBodyNumbers[number + 2],
                        Color2 = (uint)_streetBodyNumbers[number + 3],
                        Position = new Vec3((float)_streetBodyNumbers[number + 4], (float)_streetBodyNumbers[number + 5], (float)_streetBodyNumbers[number + 6]),
                        Direction = new Vec2((float)_streetBodyNumbers[number + 7], (float)_streetBodyNumbers[number + 8])
                    };
                }
            }
        }
    }
}
