﻿using Verse;

namespace AvaliMod
{
    public class AddHediff : HediffGiver
    {
        public BodyPartDef bodyPart;

        public override void OnIntervalPassed(Pawn pawn, Hediff cause)
        {
            if (!pawn.Spawned || pawn.health.hediffSet.HasHediff(hediff))
            {
                return;
            }

            BodyPartRecord bodyPartRecord = pawn.RaceProps.body.GetPartsWithDef(bodyPart).RandomElement();
            pawn.health.AddHediff(hediff, bodyPartRecord);
            Log.Message($"Added hediff {hediff} to pawn {pawn.Name}");
        }
    }
}
