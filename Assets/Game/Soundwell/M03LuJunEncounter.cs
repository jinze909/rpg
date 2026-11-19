using System;
using GamingIsLove.Makinom;

namespace Rpg.Soundwell
{
    public enum M03LuJunStage
    {
        NotStarted,
        VisitingVeterans,
        ChoosingOpening,
        RespondingToConfession,
        ReplyingToSecrecyRequest,
        ChoosingVoyage,
        Complete
    }

    public enum M03LuJunOpening
    {
        AskWhoCutBridge,
        FindMyName,
        NeedAShip
    }

    public enum M03LuConfession
    {
        NotAsked,
        EarlyQuestion,
        AskedWithEvidence
    }

    public enum M03SecrecyAnswer
    {
        NotTriggered,
        PromiseSecrecy,
        Undecided,
        RefuseSecrecy
    }

    public enum M03ConfessionResponse
    {
        YouShouldHaveToldThem,
        YouChoseForThem,
        RemainSilent
    }

    public enum M03VoyageChoice
    {
        LuJunEscort,
        QiQueMerchant,
        HanQiaoOfficial,
        MixedWithOfficial,
        MixedWithCivilian
    }

    /// <summary>
    /// Authored M03 Lu Jun camp encounter. The bridge confession only
    /// happens if the player chooses to ask. Silence or asking for a ship
    /// never implies a promise to hide something the player has not heard.
    /// The official dock requires an already stamped M02 registration.
    /// </summary>
    public sealed class M03LuJunEncounter
    {
        public M03LuJunEncounter(M02BridgeEvidence bridgeEvidence,
            bool officialRegistrationEffective)
        {
            const M02BridgeEvidence known = M02BridgeEvidence.CleanCableCut |
                M02BridgeEvidence.FerrymanLog | M02BridgeEvidence.ThirdBellSignal |
                M02BridgeEvidence.WarReportDraft;
            if ((bridgeEvidence & ~known) != M02BridgeEvidence.None)
                throw new ArgumentOutOfRangeException(nameof(bridgeEvidence));
            BridgeEvidence = bridgeEvidence;
            OfficialRegistrationEffective = officialRegistrationEffective;
        }

        public M02BridgeEvidence BridgeEvidence { get; }
        public bool OfficialRegistrationEffective { get; }
        public M03LuJunStage Stage { get; private set; }
        public M03LuJunOpening? Opening { get; private set; }
        public M03LuConfession Confession { get; private set; } = M03LuConfession.NotAsked;
        public M03ConfessionResponse? Reaction { get; private set; }
        public M03SecrecyAnswer Secrecy { get; private set; } = M03SecrecyAnswer.NotTriggered;
        public M03VoyageChoice? Voyage { get; private set; }

        public void Begin()
        {
            Require(M03LuJunStage.NotStarted);
            Stage = M03LuJunStage.VisitingVeterans;
        }

        public void ObserveRollCall()
        {
            Require(M03LuJunStage.VisitingVeterans);
            Stage = M03LuJunStage.ChoosingOpening;
        }

        public void Ask(M03LuJunOpening opening)
        {
            Require(M03LuJunStage.ChoosingOpening);
            if (!Enum.IsDefined(typeof(M03LuJunOpening), opening))
                throw new ArgumentOutOfRangeException(nameof(opening));
            Opening = opening;
            if (opening == M03LuJunOpening.AskWhoCutBridge)
            {
                Confession = BridgeEvidence == M02BridgeEvidence.None
                    ? M03LuConfession.EarlyQuestion
                    : M03LuConfession.AskedWithEvidence;
                Stage = M03LuJunStage.RespondingToConfession;
            }
            else
            {
                // No confession, no secrecy request, no forced revelation.
                Stage = M03LuJunStage.ChoosingVoyage;
            }
        }

        public void Respond(M03ConfessionResponse response)
        {
            Require(M03LuJunStage.RespondingToConfession);
            if (!Enum.IsDefined(typeof(M03ConfessionResponse), response))
                throw new ArgumentOutOfRangeException(nameof(response));
            Reaction = response;
            Stage = M03LuJunStage.ReplyingToSecrecyRequest;
        }

        public void AnswerSecrecy(M03SecrecyAnswer answer)
        {
            Require(M03LuJunStage.ReplyingToSecrecyRequest);
            if (answer != M03SecrecyAnswer.PromiseSecrecy &&
                answer != M03SecrecyAnswer.Undecided &&
                answer != M03SecrecyAnswer.RefuseSecrecy)
                throw new ArgumentOutOfRangeException(nameof(answer));
            Secrecy = answer;
            Stage = M03LuJunStage.ChoosingVoyage;
        }

        public bool CanChoose(M03VoyageChoice choice)
        {
            if (!Enum.IsDefined(typeof(M03VoyageChoice), choice))
                return false;
            if (choice == M03VoyageChoice.HanQiaoOfficial ||
                choice == M03VoyageChoice.MixedWithOfficial)
                return OfficialRegistrationEffective;
            return true;
        }

        public void ChooseVoyage(M03VoyageChoice choice)
        {
            Require(M03LuJunStage.ChoosingVoyage);
            if (!CanChoose(choice))
                throw new InvalidOperationException(
                    "An unstamped registration cannot grant access to the official dock.");
            Voyage = choice;
            Stage = M03LuJunStage.Complete;
        }

        public string VoyagePathValue
        {
            get
            {
                if (!Voyage.HasValue)
                    return null;
                switch (Voyage.Value)
                {
                    case M03VoyageChoice.LuJunEscort: return "陆钧护航";
                    case M03VoyageChoice.QiQueMerchant: return "祁雀商路";
                    case M03VoyageChoice.HanQiaoOfficial: return "韩峤船坞";
                    case M03VoyageChoice.MixedWithOfficial:
                    case M03VoyageChoice.MixedWithCivilian: return "混合";
                    default: throw new InvalidOperationException("Unknown voyage choice.");
                }
            }
        }

        private void Require(M03LuJunStage expected)
        {
            if (Stage != expected)
                throw new InvalidOperationException("M03 Lu Jun encounter is not at " + expected + ".");
        }
    }

    /// <summary>
    /// The scene host records facts only once all M03 choices are settled.
    /// Missing Makinom does not claim a successful delivery. ORK owns saves.
    /// </summary>
    public static class M03LuJunFacts
    {
        public const string ConfessionKey = "rpg.return.m03.lu-confession";
        public const string SecrecyKey = "rpg.return.m03.lu-secrecy-promise";
        public const string VoyageKey = "rpg.return.m03.voyage-path";

        internal static bool TryResetForNewVisit()
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;

            variables.Set(ConfessionKey, 0);
            variables.Set(SecrecyKey, 0);
            variables.Set(VoyageKey, 0);
            return true;
        }

        public static bool TryRecord(M03LuJunEncounter encounter)
        {
            ValidateCompleted(encounter);
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            if (variables == null)
                return false;
            return TryRecord(encounter, (key, value) => variables.Set(key, value));
        }

        public static bool TryRecord(M03LuJunEncounter encounter, Action<string, int> setInt)
        {
            ValidateCompleted(encounter);
            if (setInt == null)
                return false;

            // Invalidate any previously saved route before writing this visit's
            // answers. The route is committed last, after the dependent facts,
            // so an interrupted write cannot reactivate a previous voyage.
            setInt(VoyageKey, 0);
            setInt(ConfessionKey, (int)encounter.Confession + 1);
            setInt(SecrecyKey, (int)encounter.Secrecy + 1);
            setInt(VoyageKey, (int)encounter.Voyage.Value + 1);
            return true;
        }

        public static bool TryRead(out M03LuConfession confession,
            out M03SecrecyAnswer secrecy, out M03VoyageChoice? voyage)
        {
            var variables = Maki.Initialized ? Maki.Game?.Variables : null;
            return TryRead(variables == null ? null : (Func<string, int>)(key =>
                variables.GetInt(key)), out confession, out secrecy, out voyage);
        }

        public static bool TryRead(Func<string, int> getInt,
            out M03LuConfession confession, out M03SecrecyAnswer secrecy,
            out M03VoyageChoice? voyage)
        {
            confession = M03LuConfession.NotAsked;
            secrecy = M03SecrecyAnswer.NotTriggered;
            voyage = null;
            if (getInt == null)
                return false;

            int v = getInt(VoyageKey) - 1;
            if (!Enum.IsDefined(typeof(M03VoyageChoice), v))
                return true;

            // Only a fully committed route makes confession and secrecy facts
            // visible; interrupted writes must not expose a partial dialogue.
            int c = getInt(ConfessionKey) - 1;
            int s = getInt(SecrecyKey) - 1;
            if (!Enum.IsDefined(typeof(M03LuConfession), c) ||
                !Enum.IsDefined(typeof(M03SecrecyAnswer), s))
                return true;
            // A player who never heard the confession cannot have answered
            // its secrecy request. Conversely, every heard confession
            // requires an explicit answer before a voyage can be chosen.
            if ((c == (int)M03LuConfession.NotAsked) !=
                (s == (int)M03SecrecyAnswer.NotTriggered))
                return true;

            confession = (M03LuConfession)c;
            secrecy = (M03SecrecyAnswer)s;
            voyage = (M03VoyageChoice)v;
            return true;
        }

        private static void ValidateCompleted(M03LuJunEncounter encounter)
        {
            if (encounter == null)
                throw new ArgumentNullException(nameof(encounter));
            if (encounter.Stage != M03LuJunStage.Complete ||
                !encounter.Voyage.HasValue)
                throw new InvalidOperationException("Incomplete M03 choices cannot be saved.");
        }
    }

    public static class M03LuJunCopy
    {
        // M03's three unanswered names are three actual playable beats,
        // not a one-line summary that lets the player skip the silence.
        // No ORK/Makinom fact is awarded during the roll call.
        public static string RollCallMoment(int beat, bool chinese)
        {
            switch (beat)
            {
                case 0:
                    return chinese
                        ? "傍晚海边，旧部穿着各色便衣，袖口缝着蓝布。陆钧替缺腿老兵付医费，记得他的家人；让不愿再穿军服的木匠自由离开。入夜，他翻开每晚点三十人的旧名册。"
                        : "At dusk the old soldiers wear ordinary clothes with blue cloth at their cuffs. Lu Jun pays a disabled veteran's care and lets a carpenter leave freely. At night he opens the roll of thirty names.";
                case 1:
                    return chinese
                        ? "陆钧：第三旅，一营。赵得福。\n有人答：在。\n陆钧：钱满仓。\n（一拍沉默，没有回应。）"
                        : "Lu Jun: 'Third Brigade, First Battalion. Zhao Defu.'\nA voice: 'Here.'\nLu Jun: 'Qian Mancang.'\n(A beat of silence. No answer.)";
                case 2:
                    return chinese
                        ? "陆钧又念：钱满仓。\n（仍然没有人回答。他没有划去那个名字。）"
                        : "Lu Jun calls again: 'Qian Mancang.'\n(No answer. He does not strike out the name.)";
                case 3:
                    return chinese
                        ? "陆钧第三次念：钱满仓。\n（三遍无人应。他轻轻点了点名字，接着念。）\n老兵低声说：每晚三十个，念完整本要三十七晚。他已经念了好几遍。"
                        : "Lu Jun calls a third time: 'Qian Mancang.'\n(No answer. He taps the name and keeps reading.)\nA veteran whispers: thirty names a night, thirty-seven nights for the whole book. He has read it many times.";
                default:
                    throw new ArgumentOutOfRangeException(nameof(beat));
            }
        }

        public static string Narration(M03LuJunEncounter e, bool chinese)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            switch (e.Stage)
            {
                case M03LuJunStage.VisitingVeterans:
                    return RollCallMoment(0, chinese);
                case M03LuJunStage.ChoosingOpening:
                    return chinese
                        ? "陆钧合上名册：“你来得比我预想的早。”你可以问桥、问自己的名字，或先谈出海。"
                        : "Lu Jun closes the roll. 'You came earlier than I expected.' Ask about the bridge, the names, or passage.";
                case M03LuJunStage.RespondingToConfession:
                    return chinese
                        ? "陆钧：“总帅令。三更，工兵队断桥。第三旅不撤。我接了命令。\n" +
                          "桥这头一万四千，桥那头一千一百。我没说。说了，防线天亮前就散了。可到了天亮，还有人排队投降。\n" +
                          "我用他们的不知道，换了他们的命。这个选择，我十二年没睡好。”"
                        : "Lu Jun: 'The marshal's order. The engineers cut the bridge in the third watch. " +
                          "The Third Brigade was to hold its ground. I obeyed.\n" +
                          "Fourteen thousand on this side, eleven hundred on the other. I kept quiet. " +
                          "If I'd told them, the line would have broken before dawn. " +
                          "But come dawn, there were still men alive to line up and surrender.\n" +
                          "I traded their ignorance for their lives. I haven't slept well in twelve years.'";
                case M03LuJunStage.ReplyingToSecrecyRequest:
                    // React to the player's actual accusation before requesting secrecy.
                    string reaction;
                    switch (e.Reaction)
                    {
                        case M03ConfessionResponse.YouShouldHaveToldThem:
                            reaction = chinese
                                ? "陆钧：对。我每晚都在想这句话。现在去说，他们会问我为什么现在才说，我答不上来。"
                                : "Lu Jun: 'Yes. I think of that every night. If I tell them now, they'll ask why I waited. I have no answer.'";
                            break;
                        case M03ConfessionResponse.YouChoseForThem:
                            reaction = chinese
                                ? "陆钧：是。我到现在还在替他们决定，你看出来了。"
                                : "Lu Jun: 'Yes. I am still deciding for them. You saw it.'";
                            break;
                        case M03ConfessionResponse.RemainSilent:
                            reaction = chinese
                                ? "（陆钧等了很久，没有开口，也没有催你。）"
                                : "(Lu Jun waits a long time without speaking or pressing you.)";
                            break;
                        default:
                            throw new InvalidOperationException("The confession needs a response before secrecy.");
                    }
                    return reaction + (chinese
                        ? "\n陆钧：他们的家人以为自己的儿子是死守桥头的英雄。你拿走这个，他们还剩什么？别告诉他们。"
                        : "\nLu Jun: 'Their families believe their sons died as heroes defending the bridge. Take that away and what do they have left? Please don't tell them.'");
                case M03LuJunStage.ChoosingVoyage:
                    // The two non-bridge openings are distinct authored
                    // conversations, not a generic "didn't ask" placeholder.
                    // Neither discloses the bridge order nor triggers secrecy.
                    string preface;
                    if (e.Confession != M03LuConfession.NotAsked)
                    {
                        // The farewell belongs to the player's explicit secrecy answer.
                        switch (e.Secrecy)
                        {
                            case M03SecrecyAnswer.PromiseSecrecy:
                                preface = chinese
                                    ? "陆钧：谢谢。（停）你什么也不欠我。"
                                    : "Lu Jun: 'Thank you. (A pause.) You owe me nothing.'";
                                break;
                            case M03SecrecyAnswer.Undecided:
                                preface = chinese
                                    ? "陆钧：好。决定了告诉我一声，我不拦你。"
                                    : "Lu Jun: 'All right. Tell me when you decide. I won't stop you.'";
                                break;
                            case M03SecrecyAnswer.RefuseSecrecy:
                                preface = chinese
                                    ? "陆钧：我不会拦你。如果你决定说，先想想每一个名字后面站着几个人。"
                                    : "Lu Jun: 'I won't stop you. If you decide to tell them, think of the people behind every name.'";
                                break;
                            default:
                                throw new InvalidOperationException("A heard confession needs an explicit secrecy answer.");
                        }
                    }
                    else if (e.Opening == M03LuJunOpening.FindMyName)
                        preface = chinese
                            ? "陆钧把名册翻到你的名字：“我没划。你要我划，也得你自己说。”他把名册留在那一页。"
                            : "Lu Jun opens the roll at your name: 'I never struck it out. If you want it removed, say so yourself.' He leaves the roll open on that page.";
                    else if (e.Opening == M03LuJunOpening.NeedAShip)
                        preface = chinese
                            ? "陆钧用指尖在潮道图上划了一道：“这里有人认路。要我派护卫，船上得挂第三旅的旗。”"
                            : "Lu Jun traces a line on the tideway chart. 'I know someone who can guide you through here. If you want my escort, fly the Third Brigade flag.'";
                    else
                        preface = chinese ? "你还没有问桥，他也没有提出保密请求。" :
                            "You have not asked about the bridge, and Lu Jun has made no secrecy request.";
                    return preface + (chinese
                        ? "\n护航须公开挂第三旅旧军旗；也可选择祁雀民船，或借潮道资料与其他渠道混合。官船必须已有正式盖印。"
                        : "\nAn escort requires openly flying the Third Brigade flag. You may use Qi Que's merchant route or combine tide charts with another route. Official passage requires a stamped registration.");
                case M03LuJunStage.Complete:
                    return chinese
                        ? "你把出海渠道记在心里，接下来的准备才真正开始。"
                        : "You have chosen a passage plan. The actual departure preparations still lie ahead.";
                default:
                    throw new InvalidOperationException("Begin M03 before rendering.");
            }
        }

        public static string[] Choices(M03LuJunEncounter e, bool chinese)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            switch (e.Stage)
            {
                case M03LuJunStage.ChoosingOpening:
                    return chinese
                        ? new[] { "我来问桥。", "你念的那些名字，有我的。", "我需要一条船。" }
                        : new[] { "I came to ask about the bridge.", "My name is in your roll.", "I need a ship." };
                case M03LuJunStage.RespondingToConfession:
                    return chinese
                        ? new[] { "你该说的。", "你替他们决定了。", "（沉默）" }
                        : new[] { "You should have told them.", "You chose for them.", "(Remain silent)" };
                case M03LuJunStage.ReplyingToSecrecyRequest:
                    return chinese
                        ? new[] { "我不会告诉他们。", "我还没决定。", "我不能答应你。" }
                        : new[] { "I won't tell them.", "I haven't decided.", "I can't promise that." };
                case M03LuJunStage.ChoosingVoyage:
                    return chinese
                        ? new[] { "陆钧护航（公开旧军旗）", "祁雀的隐蔽商路", "韩峤官船（须已盖印）",
                            "官船加潮道情报（须已盖印）", "民船加潮道情报" }
                        : new[] { "Lu Jun escort (public flag)", "Qi Que merchant route",
                            "Han Qiao official ship (requires stamp)",
                            "Official ship with tide charts (requires stamp)", "Civilian ship with tide charts" };
                default:
                    return Array.Empty<string>();
            }
        }
    }
}
