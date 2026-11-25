using System.Globalization;
using System.Text;

namespace PdfShark.Services;

/// <summary>Ключевое слово и его вес.</summary>
public sealed record KeywordHit(string Word, int Count, double Score);

/// <summary>Результат анализа текста документа.</summary>
public sealed record TextAnalysisResult(
    string Theme,
    string Summary,
    string Language,
    IReadOnlyList<KeywordHit> TopKeywords);

/// <summary>Определяет тему и краткое содержание по тексту.</summary>
public interface IThemeAnalyzer
{
    TextAnalysisResult Analyze(string text);
}

/// <summary>
/// Локальный анализатор (без внешних сервисов), русский + английский.
/// <para>
/// Тема — топ ключевых слов: частота с поправкой на длину слова, после отсева
/// стоп-слов и чисел.
/// </para>
/// <para>
/// Краткое содержание — extractive-выборка до 3 предложений с максимальной
/// «плотностью» ключевых слов, с небольшой надбавкой за позицию (начало документа
/// немного весомее) и устранением повторов (greedy MMR).
/// </para>
/// </summary>
public sealed class SimpleTextAnalyzer : IThemeAnalyzer
{
    private const int MaxSummaryLength = 600;
    private const int MaxThemeWords = 3;
    private const int MinSentenceLength = 15;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // ---- Русские стоп-слова ----
        "и", "в", "во", "не", "что", "он", "на", "я", "с", "со", "как", "а", "то",
        "все", "она", "так", "его", "но", "да", "ты", "к", "у", "же", "вы", "за",
        "бы", "по", "только", "ее", "мне", "было", "вот", "от", "меня", "еще",
        "нет", "о", "из", "ему", "теперь", "когда", "даже", "ну", "вдруг", "ли",
        "если", "уже", "или", "ни", "быть", "был", "него", "до", "вас", "нибудь",
        "опять", "уж", "вам", "ведь", "там", "потом", "себя", "ничего", "ей",
        "может", "они", "тут", "где", "есть", "надо", "ней", "для", "мы", "тебя",
        "их", "чем", "была", "сам", "чтоб", "без", "будто", "чего", "раз", "тоже",
        "себе", "под", "будет", "ж", "тогда", "кто", "этот", "того", "потому",
        "этого", "какой", "совсем", "ним", "здесь", "этом", "один", "почти", "мой",
        "тем", "чтобы", "нее", "сейчас", "были", "куда", "зачем", "всех", "никогда",
        "можно", "при", "наконец", "два", "об", "другой", "хоть", "после", "над",
        "больше", "тот", "через", "эти", "нас", "про", "всего", "них", "какая",
        "много", "разве", "три", "эту", "моя", "впрочем", "хорошо", "свою", "этой",
        "перед", "иногда", "лучше", "чуть", "том", "нельзя", "такой", "им", "более",
        "всегда", "конечно", "всю", "между", "без", "должен", "должна", "должны",
        "является", "являются", "будет", "будь", "при", "это", "также", "такие",
        "такая", "такое", "таких", "данном", "данных", "данные", "данный", "данное",
        "данной", "части", "часть", "случае", "случай", "время", "года", "год",
        "либо", "включая", "включает", "позволяет", "позволяют", "отношении", "связи",
        "основании", "согласно", "соответствии", "средств", "средств", "помощью",
        "путем", "путем", "необходимо", "необходимость", "различных", "различные",
        "следующих", "следующее",
        // ---- Английские стоп-слова ----
        "a", "an", "and", "the", "of", "to", "in", "for", "on", "is", "are", "was",
        "were", "be", "being", "been", "it", "its", "this", "that", "these", "those",
        "i", "you", "he", "she", "we", "they", "them", "me", "him", "her", "us",
        "my", "your", "his", "their", "our", "as", "at", "by", "with", "from", "or",
        "but", "not", "no", "so", "if", "than", "then", "there", "here", "when",
        "where", "which", "who", "whom", "whose", "what", "can", "could", "would",
        "should", "may", "might", "must", "will", "shall", "do", "does", "did",
        "have", "has", "had", "into", "out", "up", "down", "off", "over", "under",
        "about", "after", "before", "between", "during", "against", "other", "some",
        "any", "each", "every", "both", "all", "few", "more", "most", "such", "only",
        "also", "very", "just", "too", "now", "even", "still", "well", "one", "two",
        "their", "her", "him", "its", "according", "based", "following", "various",
        "including", "therefore", "however", "moreover", "furthermore", "additionally",
        "because", "although", "within", "without", "among", "between"
    };

    public TextAnalysisResult Analyze(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TextAnalysisResult(string.Empty, string.Empty, "unknown", Array.Empty<KeywordHit>());
        }

        string language = DetectLanguage(text);

        // 1. Частотность значимых слов.
        var freq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (string w in Tokenize(text))
        {
            if (!IsMeaningful(w))
            {
                continue;
            }
            freq[w] = freq.TryGetValue(w, out int c) ? c + 1 : 1;
        }

        var topKeywords = freq
            .Select(kv => new KeywordHit(kv.Key, kv.Value, Score(kv.Key, kv.Value)))
            .OrderByDescending(k => k.Score)
            .ThenBy(k => k.Word, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        if (topKeywords.Count == 0)
        {
            return new TextAnalysisResult(string.Empty, string.Empty, language, Array.Empty<KeywordHit>());
        }

        var kwSet = topKeywords.Select(k => k.Word).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string theme = BuildTheme(topKeywords);
        string summary = BuildSummary(text, kwSet);

        return new TextAnalysisResult(theme, summary, language, topKeywords);
    }

    /// <summary>Вес слова: частота с умеренной надбавкой за длину (длинные значимее).</summary>
    private static double Score(string word, int count)
        => count * Math.Log(1 + Math.Min(word.Length, 12));

    /// <summary>Значимое слово: не слишком короткое, не число, не стоп-слово.</summary>
    private static bool IsMeaningful(string word)
        => word.Length >= 3 &&
           !StopWords.Contains(word) &&
           !IsNumeric(word);

    private static bool IsNumeric(string word)
        => int.TryParse(word, out _) ||
           double.TryParse(word, NumberStyles.Any, CultureInfo.InvariantCulture, out _);

    /// <summary>Тема документа — топ ключевых слов (с большой буквы).</summary>
    private static string BuildTheme(IReadOnlyList<KeywordHit> top)
    {
        int n = Math.Min(top.Count, MaxThemeWords);
        var parts = new List<string>(n);
        for (int i = 0; i < n; i++)
        {
            parts.Add(Capitalize(top[i].Word));
        }
        return string.Join(" ", parts);
    }
/// <summary>Токенизация: слова из букв (кириллица/латиница), приводятся к нижнему регистру.</summary>
    private static IEnumerable<string> Tokenize(string text)
    {
        var result = new List<string>();
        var cur = new StringBuilder();
        foreach (char ch in text)
        {
            if (IsWordChar(ch))
            {
                cur.Append(char.ToLowerInvariant(ch));
            }
            else if (cur.Length > 0)
            {
                result.Add(cur.ToString());
                cur.Clear();
            }
        }
        if (cur.Length > 0)
        {
            result.Add(cur.ToString());
        }
        return result;
    }

    private static bool IsWordChar(char ch)
        => char.IsLetter(ch) || ch == '\u2019' || ch == '\'';

    /// <summary>Грубое определение языка: ru / en / mixed / unknown.</summary>
    private static string DetectLanguage(string text)
    {
        int cyr = 0, lat = 0;
        foreach (char ch in text)
        {
            if (ch >= '\u0400' && ch <= '\u04FF')
            {
                cyr++;
            }
            else if ((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z'))
            {
                lat++;
            }
        }
        if (cyr == 0 && lat == 0)
        {
            return "unknown";
        }
        return cyr > lat ? "ru" : (lat > cyr ? "en" : "mixed");
    }

    /// <summary>Extractive-краткое содержание: до 3 предложений, MMR-выбор без дублей.</summary>
    private static string BuildSummary(string text, IReadOnlyCollection<string> kwSet)
    {
        var sentences = SplitSentences(text);
        if (sentences.Count == 0)
        {
            return string.Empty;
        }

        var pool = new List<Sen>();
        for (int i = 0; i < sentences.Count; i++)
        {
            string s = Clean(sentences[i]);
            if (s.Length < MinSentenceLength)
            {
                continue;
            }
            var words = Tokenize(s);
            double kwScore = 0;
            foreach (string w in words)
            {
                if (kwSet.Contains(w))
                {
                    kwScore += 1;
                }
            }
            if (kwScore == 0)
            {
                continue;
            }
            // Небольшая надбавка за близость к началу документа.
            double position = 1.0 + 0.3 * Math.Exp(-3.0 * ((double)i / Math.Max(1, sentences.Count)));
            pool.Add(new Sen(i, s, kwScore * position));
        }

        if (pool.Count == 0)
        {
            // Нет «весомых» предложений — просто показываем первые связные предложения.
            var head = sentences.Take(3).Select(s => Clean(s)).Where(s => s.Length >= MinSentenceLength).Take(3);
            return Truncate(string.Join(" ", head), MaxSummaryLength);
        }

        // Greedy выбор с устранением избыточности (MMR).
        var picked = new List<Sen>();
        while (picked.Count < 3)
        {
            var best = pool.OrderByDescending(s => s.Score).FirstOrDefault();
            if (best is null || best.Score <= 0)
            {
                break;
            }
            picked.Add(best);
            pool.Remove(best);
            foreach (var s in pool)
            {
                double overlap = Overlap(best.Text, s.Text);
                s.Score -= 0.6 * s.Score * overlap;
            }
        }

        var ordered = picked.OrderBy(s => s.Index).Select(s => s.Text);
        return Truncate(string.Join(" ", ordered), MaxSummaryLength);
    }

    /// <summary>Доля общих слов между двумя предложениями (для MMR-штрафа).</summary>
    private static double Overlap(string a, string b)
    {
        var setA = Tokenize(a).Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var setB = Tokenize(b).Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int shared = setA.Count(setB.Contains);
        return shared / (double)Math.Max(1, Math.Min(setA.Count, setB.Count));
    }

    private sealed class Sen
    {
        public int Index { get; }
        public string Text { get; }
        public double Score { get; set; }
        public Sen(int index, string text, double score)
        {
            Index = index;
            Text = text;
            Score = score;
        }
    }
/// <summary>
    /// Разбитие на предложения по [.!?\n\r]. Не разрывает внутри чисел (3.14),
    /// сокращений и точек между буквами без пробелов (напр. «т.е.», «г.Москва»).
    /// </summary>
    private static IReadOnlyList<string> SplitSentences(string text)
    {
        var list = new List<string>();
        var sb = new StringBuilder();
        int n = text.Length;

        for (int i = 0; i < n; i++)
        {
            char c = text[i];
            sb.Append(c);

            if (c == '\n' || c == '\r' || c == '!' || c == '?')
            {
                Flush(list, sb);
                continue;
            }
            if (c == '.')
            {
                if (IsSentenceEnd(text, i))
                {
                    Flush(list, sb);
                }
            }
        }
        if (sb.Length > 0)
        {
            Flush(list, sb);
        }
        return list;
    }

    private static bool IsSentenceEnd(string text, int i)
    {
        char prev = i > 0 ? text[i - 1] : '\0';
        char next = i + 1 < text.Length ? text[i + 1] : '\0';

        // Десятичная точка "3.14" — не конец предложения.
        if (char.IsDigit(prev) && char.IsDigit(next))
        {
            return false;
        }
        // Точка между буквами без пробела ("т.е.", "г.Москва", "and.e.g.") — не разделяем.
        if (char.IsLetter(prev) && char.IsLetter(next))
        {
            return false;
        }
        // Точка в конце слова, за которой сразу идёт буква — продолжение (инициалы/сокращение) — не разделяем.
        if (char.IsLetter(prev) && (next == ' ' || next == '\0' || next == '\t' || i + 1 >= text.Length))
        {
            return true;
        }
        return false;
    }

    private static void Flush(List<string> list, StringBuilder sb)
    {
        string s = sb.ToString().Trim();
        if (s.Length > 0)
        {
            list.Add(s);
        }
        sb.Clear();
    }

    private static string Clean(string s)
    {
        foreach (char c in new[] { '\r', '\n', '\t' })
        {
            s = s.Replace(c.ToString(), " ");
        }
        return NormalizeSpaces(s);
    }

    private static string NormalizeSpaces(string s)
    {
        var sb = new StringBuilder(s.Length);
        bool inSpace = false;
        foreach (char c in s)
        {
            if (c == ' ')
            {
                if (inSpace)
                {
                    continue;
                }
                inSpace = true;
                sb.Append(c);
            }
            else
            {
                inSpace = false;
                sb.Append(c);
            }
        }
        return sb.ToString().Trim();
    }

    private static string Capitalize(string word)
        => string.IsNullOrEmpty(word) ? word : char.ToUpperInvariant(word[0]) + word[1..];

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max].TrimEnd() + "…";
}