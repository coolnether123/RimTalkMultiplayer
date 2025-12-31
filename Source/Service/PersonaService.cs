using System;
using System.Threading.Tasks;
using Multiplayer.API;
using RimTalk.Service;
using RimTalk.Util;
using Verse;

namespace RimTalk.Data;

public static class PersonaService
{
    public static string GetPersonality(Pawn pawn)
    {
        return Hediff_Persona.GetOrAddNew(pawn).Personality;
    }

    [SyncMethod]
    public static void SetPersonality(Pawn pawn, string personality)
    {
        Hediff_Persona.GetOrAddNew(pawn).Personality = personality;
    }

    public static float GetTalkInitiationWeight(Pawn pawn)
    {
        return Hediff_Persona.GetOrAddNew(pawn).TalkInitiationWeight;
    }

    [SyncMethod]
    public static void SetTalkInitiationWeight(Pawn pawn, float frequency)
    {
        Hediff_Persona.GetOrAddNew(pawn).TalkInitiationWeight = frequency;
    }

    public static async Task<PersonalityData> GeneratePersona(Pawn pawn)
    {
        string pawnBackstory = PromptService.CreatePawnBackstory(pawn, PromptService.InfoLevel.Full);

        try
        {
            var request = new TalkRequest(Constant.PersonaGenInstruction, pawn)
            {
                Context = $"[Character]\n{pawnBackstory}"
            };
            PersonalityData personalityData = await AIService.Query<PersonalityData>(request);

            if (personalityData?.Persona != null)
            {
                personalityData.Persona = personalityData.Persona.Replace("**", "").Trim();
            }

            return personalityData;
        }
        catch (Exception e)
        {
            Logger.Error(e.Message);
            return null;
        }
    }
}