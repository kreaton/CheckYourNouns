namespace CheckYourNouns.Game;

/// <summary>One gender (or, for English, article) a noun can have.</summary>
/// <param name="Code">The code used in nouns.json: "en", "der", "m", "a" …</param>
/// <param name="Label">What the answer button shows: the article.</param>
/// <param name="Name">The grammatical name, shown under the article when the word isn't (Romance languages).</param>
public sealed record Gender(string Code, string Label, string? Name = null);

/// <summary>A noun in one language: its gender code and the word without article.</summary>
public sealed record Term(string Gender, string Word);

public sealed class Language
{
    public required string Code { get; init; }

    /// <summary>The language's own name for itself, as shown in the pickers.</summary>
    public required string Name { get; init; }

    public required string[] KeyboardRows { get; init; }

    public required Gender[] Genders { get; init; }

    /// <summary>The question asked once the word is spelled.</summary>
    public string Question { get; init; } = "Which gender?";

    /// <summary>Gender codes that are also accepted for a noun of the given gender (Norwegian ei → en).</summary>
    public IReadOnlyDictionary<string, string[]> AlsoAccepted { get; init; } = new Dictionary<string, string[]>();

    /// <summary>Builds the noun with its definite article, for languages where it depends on the word.</summary>
    private Func<Term, string>? ArticleRule { get; init; }

    private HashSet<char>? _keys;
    private HashSet<char> Keys => _keys ??= KeyboardRows.SelectMany(r => r).ToHashSet();

    /// <summary>Whether the answer buttons show "article word" (der Mann) rather than article and gender name (le · masculin).</summary>
    public bool ArticleWithWord => ArticleRule is null;

    public Gender GenderOf(string code) => Genders.First(g => g.Code == code);

    public bool Accepts(Term term, string gender) =>
        gender == term.Gender || (AlsoAccepted.TryGetValue(term.Gender, out var also) && also.Contains(gender));

    /// <summary>The noun as it is said with its article: "der Mann", "an apple", "l'eau", "lo zaino".</summary>
    public string WithArticle(Term term) =>
        ArticleRule?.Invoke(term) ?? $"{GenderOf(term.Gender).Label} {term.Word}";

    /// <summary>The word as the capital letters to type. Œ is spelled as OE.</summary>
    public static string LettersOf(Term term) => term.Word.Trim().ToUpperInvariant().Replace("Œ", "OE");

    /// <summary>The keyboard key that types this character: accents not on the keyboard are typed without.</summary>
    public char KeyFor(char c)
    {
        var upper = char.ToUpperInvariant(c);
        if (Keys.Contains(upper)) return upper;
        foreach (var (accented, plain) in Folds)
            if (accented.Contains(upper)) return plain;
        return upper;
    }

    /// <summary>Whether every letter of the word can be typed on this language's keyboard.</summary>
    public bool CanType(Term term) => LettersOf(term).Where(char.IsLetter).All(c => Keys.Contains(KeyFor(c)));

    // Written as a table rather than with Unicode normalization, which needs ICU data in WebAssembly.
    private static readonly (string Accented, char Plain)[] Folds =
    [
        ("ÀÁÂÃÄÅĀ", 'A'), ("ÇĆČ", 'C'), ("ÈÉÊËĒĖĘ", 'E'), ("ÌÍÎÏ", 'I'), ("ÑŃ", 'N'),
        ("ÒÓÔÕÖØ", 'O'), ("ÙÚÛÜ", 'U'), ("ÝŸ", 'Y'),
    ];

    private static bool StartsWithVowel(string word, bool hIsVowel)
    {
        var first = char.ToLowerInvariant(word[0]);
        // y is left out: le yaourt, lo yogurt.
        return "aàâäeéèêëiîïoôöuùûüœæ".Contains(first) || (hIsVowel && first == 'h');
    }

    // French words starting with an "h aspiré" that keep le/la instead of l'.
    private static readonly HashSet<string> HAspire = new(StringComparer.OrdinalIgnoreCase)
    {
        "haine", "hanche", "héros", "honte", "hamburger", "hasard", "haricot", "hibou", "homard",
        "hockey", "hache", "hall", "hamac", "hangar", "harpe", "haut", "hauteur", "hérisson", "housse",
    };

    // Feminine Spanish nouns starting with a stressed a, which take el in the singular.
    private static readonly HashSet<string> SpanishElFeminine = new(StringComparer.OrdinalIgnoreCase)
    {
        "agua", "alma", "arma", "ala", "área", "hambre", "águila", "hacha", "aula", "hada", "haba", "habla", "ancla", "arca",
    };

    private static string French(Term t)
    {
        var head = t.Word.Split(' ', '-')[0];
        if (StartsWithVowel(t.Word, hIsVowel: true) && !HAspire.Contains(head)) return "l'" + t.Word;
        return (t.Gender == "m" ? "le " : "la ") + t.Word;
    }

    private static string Italian(Term t)
    {
        if (StartsWithVowel(t.Word, hIsVowel: true)) return "l'" + t.Word;
        if (t.Gender == "f") return "la " + t.Word;
        var w = t.Word.ToLowerInvariant();
        var lo = (w[0] == 's' && w.Length > 1 && !"aeiouàèéìòù".Contains(w[1]))
                 || w[0] is 'z' or 'x' or 'y' || w.StartsWith("gn") || w.StartsWith("ps") || w.StartsWith("pn");
        return (lo ? "lo " : "il ") + t.Word;
    }

    private static string Spanish(Term t) =>
        (t.Gender == "m" || SpanishElFeminine.Contains(t.Word.Split(' ')[0]) ? "el " : "la ") + t.Word;

    private static readonly string[] NordicDa = ["QWERTYUIOPÅ", "ASDFGHJKLÆØ", "ZXCVBNM"];
    private static readonly string[] Qwerty = ["QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM"];

    /// <summary>The languages in the order the pickers show them.</summary>
    public static readonly IReadOnlyList<Language> All =
    [
        new()
        {
            Code = "da", Name = "Dansk", KeyboardRows = NordicDa,
            Genders = [new("en", "en"), new("et", "et")],
        },
        new()
        {
            Code = "en", Name = "English", KeyboardRows = Qwerty,
            Genders = [new("a", "a"), new("an", "an")], Question = "A or an?",
        },
        new()
        {
            Code = "de", Name = "Deutsch", KeyboardRows = ["QWERTZUIOPÜ", "ASDFGHJKLÖÄ", "YXCVBNMß"],
            Genders = [new("der", "der"), new("die", "die"), new("das", "das")],
        },
        new()
        {
            Code = "fr", Name = "Français", KeyboardRows = ["AZERTYUIOP", "QSDFGHJKLM", "WXCVBN"],
            Genders = [new("m", "le", "masculin"), new("f", "la", "féminin")], ArticleRule = French,
        },
        new()
        {
            Code = "it", Name = "Italiano", KeyboardRows = Qwerty,
            Genders = [new("m", "il", "maschile"), new("f", "la", "femminile")], ArticleRule = Italian,
        },
        new()
        {
            Code = "es", Name = "Español", KeyboardRows = ["QWERTYUIOP", "ASDFGHJKLÑ", "ZXCVBNM"],
            Genders = [new("m", "el", "masculino"), new("f", "la", "femenino")], ArticleRule = Spanish,
        },
        new()
        {
            Code = "sv", Name = "Svenska", KeyboardRows = ["QWERTYUIOPÅ", "ASDFGHJKLÖÄ", "ZXCVBNM"],
            Genders = [new("en", "en"), new("ett", "ett")],
        },
        new()
        {
            Code = "no", Name = "Norsk", KeyboardRows = NordicDa,
            Genders = [new("en", "en"), new("ei", "ei"), new("et", "et")],
            // Bokmål lets every feminine noun take en as well: ei jente or en jente.
            AlsoAccepted = new Dictionary<string, string[]> { ["ei"] = ["en"] },
        },
    ];

    public static Language Get(string code) => All.FirstOrDefault(l => l.Code == code) ?? All[0];
}
