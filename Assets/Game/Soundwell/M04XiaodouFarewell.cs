using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    public enum M04FarewellStage
    {
        NotStarted,
        Greeting,
        ChoosingTrueLine,
        HearingResponse,
        Complete
    }

    public enum M04XiaodouAnswer
    {
        TeachRealLine,
        KeepStageLine
    }

    // One independent pre-departure encounter; it never grants a ship,
    // invents a passenger, or advances the voyage without the M04 host.
    public sealed class M04XiaodouFarewell
    {
        public M04XiaodouFarewell(bool promptedOnStage)
        {
            PromptedOnStage = promptedOnStage;
        }

        public bool PromptedOnStage { get; }
        public M04FarewellStage Stage { get; private set; }
        public M04XiaodouAnswer? Answer { get; private set; }
        public bool TaughtRealLine => Answer == M04XiaodouAnswer.TeachRealLine;

        public void Begin()
        {
            Require(M04FarewellStage.NotStarted);
            Stage = M04FarewellStage.Greeting;
        }

        public void ReceiveCandy()
        {
            Require(M04FarewellStage.Greeting);
            Stage = M04FarewellStage.ChoosingTrueLine;
        }

        public void AnswerChild(M04XiaodouAnswer answer)
        {
            Require(M04FarewellStage.ChoosingTrueLine);
            if (!Enum.IsDefined(typeof(M04XiaodouAnswer), answer))
                throw new ArgumentOutOfRangeException(nameof(answer));
            Answer = answer;
            Stage = M04FarewellStage.HearingResponse;
        }

        public void SayGoodbye()
        {
            Require(M04FarewellStage.HearingResponse);
            Stage = M04FarewellStage.Complete;
        }

        private void Require(M04FarewellStage expected)
        {
            if (Stage != expected)
                throw new InvalidOperationException("M04 farewell is not at " + expected + ".");
        }
    }

    public static class M04XiaodouFarewellFacts
    {
        public const string RealLineKey = "rpg.return.m04.xiaodou-true-line";

        internal static bool TryResetForNewVisit()
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;
            // A replay cannot inherit last voyage's taught/untaught answer.
            variables.Set(RealLineKey, 0);
            return true;
        }

        public static bool TryRecord(M04XiaodouFarewell farewell)
        {
            if (farewell == null) throw new ArgumentNullException(nameof(farewell));
            if (farewell.Stage != M04FarewellStage.Complete || !farewell.Answer.HasValue)
                throw new InvalidOperationException("Do not record unfinished M04 dialogue.");
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                return false;
            // 1 = actually taught; 2 = explicitly kept the stage line.
            Maki.Game.Variables.Set(RealLineKey, farewell.TaughtRealLine ? 1 : 2);
            return true;
        }

        public static bool TryRead(out bool taught)
        {
            taught = false;
            if (!Maki.Initialized || Maki.Game?.Variables == null)
                return false;
            taught = Maki.Game.Variables.GetInt(RealLineKey) == 1;
            return true;
        }
    }

    public static class M04XiaodouFarewellCopy
    {
        public static string Describe(M04XiaodouFarewell farewell, bool chinese)
        {
            if (farewell == null) throw new ArgumentNullException(nameof(farewell));
            switch (farewell.Stage)
            {
                case M04FarewellStage.Greeting:
                    return chinese
                        ? "临潮港，出发前夜。船工们在唱歌。小豆从街口跑来，递给你一包糖人：奶奶说，坐船的人嘴里苦，吃点甜的。"
                        : "The night before departure, the crew sings at the dock. Xiaodou runs up with a wrapped sugar figure. Grandma says sailors could use something sweet.";
                case M04FarewellStage.ChoosingTrueLine:
                    return chinese
                        ? "小豆：那句“桥在我在”，我练了二十遍，再也不会忘了！你要告诉她白水渡那晚真正喊的话吗？"
                        : "Xiaodou: 'While the bridge stands, so do I! I practised it twenty times!' Will you tell her what you actually shouted that night?";
                case M04FarewellStage.HearingResponse:
                    if (farewell.TaughtRealLine)
                    {
                        if (chinese)
                        {
                            string reply = farewell.PromptedOnStage
                                ? "小豆低头看糖人：“你那天帮我提词，原来是你自己喊的？‘谁带了多余的绳子’听着不像大将军说的。”"
                                : "小豆愣了一下：“谁带了多余的绳子？那听着不像大将军说的。”";
                            return reply + "\n你：“最后三个掉队的人已经过了桥，主缆还绷着。我们得找根绳子稳住主缆，让桥再撑一会儿。”" +
                                "\n小豆点点头：“等我长大，我照这句演。”";
                        }

                        string response = farewell.PromptedOnStage
                            ? "Xiaodou looks down. 'You prompted me with your own words that day? Who has spare rope doesn't sound like a general!'"
                            : "Xiaodou blinks. 'Who has spare rope? That doesn't sound like a general!'";
                        return response + "\nYou say, 'The last three stragglers were already across. The main cable was still taut. We needed spare rope to steady it and keep the bridge up a little longer.'" +
                            "\nXiaodou nods. 'When I grow up, I'll use that line.'";
                    }
                    return chinese
                        ? "你：背得好。替我谢谢姜婶，早点回家。小豆点头，祝你一路顺风，抱着糖纸跑回西巷。"
                        : "You praise her performance and ask her to thank Grandma. Xiaodou wishes you safe passage and hurries home.";
                case M04FarewellStage.Complete:
                    return chinese ? "你收好糖人，回到出发前的准备。" :
                        "You keep the sweet and return to departure preparations.";
                default:
                    throw new InvalidOperationException("Begin M04 before describing it.");
            }
        }

        public static string[] Choices(M04XiaodouFarewell farewell, bool chinese)
        {
            if (farewell == null) throw new ArgumentNullException(nameof(farewell));
            if (farewell.Stage != M04FarewellStage.ChoosingTrueLine)
                return Array.Empty<string>();
            return chinese
                ? new[] { "其实那天我喊的是“谁带了多余的绳子”。", "背得好。替我谢谢姜婶。" }
                : new[] { "I actually shouted, 'Who has spare rope?'", "Well recited. Thank Grandma for me." };
        }
    }
}
