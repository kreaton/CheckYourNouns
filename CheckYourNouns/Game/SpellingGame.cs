using System.Text.Json;

namespace CheckYourNouns.Game;

/// <summary>One entry in nouns.json: the same noun in every language.</summary>
public sealed class Noun
{
    /// <summary>Language code → term, parsed from "gender word" (e.g. "der Mann", "f eau").</summary>
    public required IReadOnlyDictionary<string, Term> Terms { get; init; }

    /// <summary>A fuller prompt than the bare word, e.g. "cousin (male)".</summary>
    public IReadOnlyDictionary<string, string> Labels { get; init; } = new Dictionary<string, string>();

    /// <summary>Other translations shown as "also: …" under the prompt.</summary>
    public IReadOnlyDictionary<string, string[]> Also { get; init; } = new Dictionary<string, string[]>();

    public static List<Noun> ParseAll(JsonElement root)
    {
        var nouns = new List<Noun>();
        foreach (var entry in root.EnumerateArray())
        {
            var terms = new Dictionary<string, Term>();
            var labels = new Dictionary<string, string>();
            var also = new Dictionary<string, string[]>();
            foreach (var prop in entry.EnumerateObject())
            {
                if (prop.Name == "label")
                {
                    foreach (var l in prop.Value.EnumerateObject()) labels[l.Name] = l.Value.GetString() ?? "";
                }
                else if (prop.Name == "also")
                {
                    foreach (var a in prop.Value.EnumerateObject())
                        also[a.Name] = a.Value.EnumerateArray().Select(x => x.GetString() ?? "").ToArray();
                }
                else if (prop.Value.ValueKind == JsonValueKind.String
                         && prop.Value.GetString()!.Split(' ', 2) is [var gender, var word])
                {
                    terms[prop.Name] = new Term(gender, word.Trim());
                }
            }
            nouns.Add(new Noun { Terms = terms, Labels = labels, Also = also });
        }
        return nouns;
    }
}

/// <summary>A noun to play from one language into another.</summary>
public sealed record Card(string Prompt, string[] Also, Term Answer)
{
    public string Letters { get; } = Language.LettersOf(Answer);
}

public enum Stage { Spelling, Gender, Done }

public sealed class SpellingGame
{
    private readonly Random _random = new();
    private readonly System.Diagnostics.Stopwatch _watch = new();
    private readonly Queue<Card> _deck = new();
    private IReadOnlyList<Noun> _nouns = [];
    private IReadOnlyList<Card> _cards = [];

    public Language From { get; private set; } = Language.Get("en");
    public Language To { get; private set; } = Language.Get("da");

    public Card? Current { get; private set; }

    public Stage Stage { get; private set; }

    /// <summary>How many characters of the current word are filled in.</summary>
    public int Filled { get; private set; }

    /// <summary>The position of the letter the ? button revealed, if any.</summary>
    public int? HintAt { get; private set; }

    /// <summary>Whether this word has had a wrong letter, a wrong gender or a hint.</summary>
    public bool Failed { get; private set; }

    public string? WrongGender { get; private set; }

    /// <summary>Seconds allowed per word for the next round; null means no timer.</summary>
    public int? TimerSeconds { get; set; }

    /// <summary>Seconds allowed for the current word, fixed when the word starts.</summary>
    public int? TimeLimit { get; private set; }

    /// <summary>Whether the current word ran out of time.</summary>
    public bool TimedOut { get; private set; }

    /// <summary>Time spent on the current word; it stops when the word is finished.</summary>
    public TimeSpan Elapsed => _watch.Elapsed;

    /// <summary>Seconds left on the current word, or null without a timer.</summary>
    public double? Remaining => TimeLimit is { } limit ? Math.Max(0, limit - Elapsed.TotalSeconds) : null;

    /// <summary>Points for the current word once it is finished.</summary>
    public int LastPoints { get; private set; }

    public int Points { get; private set; }

    public int Correct { get; private set; }
    public int Wrong { get; private set; }
    public int Streak { get; private set; }
    public int BestStreak { get; private set; }

    public bool HasWords => _cards.Count > 0;

    public char? NextLetter => Current is null || Stage != Stage.Spelling ? null : Current.Letters[Filled];

    public void SetNouns(IReadOnlyList<Noun> nouns)
    {
        _nouns = nouns;
        BuildCards();
    }

    public void SetLanguages(Language from, Language to)
    {
        From = from;
        To = to;
        Current = null;
        BuildCards();
    }

    /// <summary>
    /// Makes the cards for the current pair. Words whose letters can't all be typed on the target
    /// keyboard are left out; other characters, such as spaces and hyphens, are filled in automatically.
    /// </summary>
    private void BuildCards()
    {
        _cards = _nouns
            .Where(n => n.Terms.ContainsKey(From.Code) && n.Terms.ContainsKey(To.Code))
            .Select(n =>
            {
                var prompt = n.Terms[From.Code];
                return new Card(
                    n.Labels.GetValueOrDefault(From.Code) ?? prompt.Word,
                    n.Also.GetValueOrDefault(From.Code) ?? [],
                    n.Terms[To.Code]);
            })
            .Where(c => c.Letters.Count(char.IsLetter) > 1
                        && To.Genders.Any(g => g.Code == c.Answer.Gender)
                        && To.CanType(c.Answer))
            .ToList();
        _deck.Clear();
    }

    public void SetScore(int correct, int wrong, int bestStreak, int points)
    {
        Correct = correct;
        Wrong = wrong;
        BestStreak = bestStreak;
        Points = points;
        Streak = 0;
    }

    public void ResetScore() => SetScore(0, 0, 0, 0);

    /// <summary>
    /// Points for a word answered without mistakes: 10 per letter, plus a speed bonus of up to as much
    /// again. The bonus shrinks linearly to 0 at the time limit, or without a timer at 1 second per
    /// letter plus 3 seconds for the gender.
    /// </summary>
    public static int ScoreFor(int letters, TimeSpan elapsed, int? timeLimit)
    {
        var basePoints = 10 * letters;
        var target = timeLimit ?? letters + 3.0;
        var speed = Math.Clamp(1 - elapsed.TotalSeconds / target, 0, 1);
        return basePoints + (int)Math.Round(basePoints * speed);
    }

    /// <summary>Ends the word as missed if its time is up. Returns true when it just timed out.</summary>
    public bool CheckTimeout()
    {
        if (Current is null || Stage == Stage.Done || Remaining is not 0) return false;

        _watch.Stop();
        TimedOut = true;
        Failed = true;
        HintAt = null;
        WrongGender = null;
        Filled = Current.Letters.Length;
        Stage = Stage.Done;
        LastPoints = 0;
        Wrong++;
        Streak = 0;
        return true;
    }

    /// <summary>Moves to the next word. Every word comes once before any repeats, in random order.</summary>
    public void NewRound()
    {
        if (_cards.Count == 0) return;

        if (_deck.Count == 0)
        {
            var shuffled = _cards.ToArray();
            _random.Shuffle(shuffled);
            if (shuffled.Length > 1 && shuffled[0] == Current)
                (shuffled[0], shuffled[^1]) = (shuffled[^1], shuffled[0]);
            foreach (var card in shuffled) _deck.Enqueue(card);
        }

        Current = _deck.Dequeue();
        Stage = Stage.Spelling;
        HintAt = null;
        Failed = false;
        WrongGender = null;
        TimedOut = false;
        LastPoints = 0;
        TimeLimit = TimerSeconds;
        // The first letter is given.
        Filled = 1;
        SkipNonLetters();
        _watch.Restart();
    }

    /// <summary>Fills in the next box if the letter is right; a wrong one fails the word.</summary>
    public bool TryLetter(char letter)
    {
        if (CheckTimeout() || NextLetter is not { } next) return false;
        if (To.KeyFor(next) != To.KeyFor(letter))
        {
            Failed = true;
            return false;
        }

        Filled++;
        HintAt = null;
        SkipNonLetters();
        return true;
    }

    /// <summary>Shows the next letter in its box, which then still has to be typed. Fails the word.</summary>
    public void Hint()
    {
        if (Stage != Stage.Spelling) return;
        HintAt = Filled;
        Failed = true;
    }

    /// <summary>Answers the gender question. The word is finished once a right one is picked.</summary>
    public bool TryGender(string gender)
    {
        if (Stage != Stage.Gender || Current is null || CheckTimeout()) return false;
        if (!To.Accepts(Current.Answer, gender))
        {
            WrongGender = gender;
            Failed = true;
            return false;
        }

        WrongGender = null;
        Stage = Stage.Done;
        _watch.Stop();
        if (Failed)
        {
            Wrong++;
            Streak = 0;
        }
        else
        {
            LastPoints = ScoreFor(Current.Letters.Count(char.IsLetter), Elapsed, TimeLimit);
            Points += LastPoints;
            Correct++;
            Streak++;
            BestStreak = Math.Max(BestStreak, Streak);
        }
        return true;
    }

    private void SkipNonLetters()
    {
        var letters = Current!.Letters;
        while (Filled < letters.Length && !char.IsLetter(letters[Filled])) Filled++;
        if (Filled == letters.Length) Stage = Stage.Gender;
    }
}
