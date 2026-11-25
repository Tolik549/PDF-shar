namespace PdfShark.Services;

/// <summary>Результат анализа текста документа.</summary>
public sealed record TextAnalysisResult(string Theme, string Summary);

/// <summary>Определяет тему и краткое содержание по тексту.</summary>
public interface IThemeAnalyzer
{
    TextAnalysisResult Analyze(string text);
}

/// <summary>
/// Простейший анализатор без внешних сервисов:
/// тема — самые частотные значимые слова (после удаления стоп-слов),
/// краткое содержание — наиболее «весомые» предложения (extractive summary).
/// Поддерживает русский и английский.
/// </summary>
public sealed class SimpleTextAnalyzer : IThemeAnalyzer
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Русские стоп-слова
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
        "всегда", "конечно", "всю", "между",
        // Английские стоп-слова
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
        "their", "her", "him", "its"
    };

    private static readonly char[] SentenceSeparators = { '.', '!', '?', '\n', '\r' };

    public TextAnalysisResult Analyze(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TextAnalysisResult(string.Empty, string.Empty);
        }

        // 1. Токенизация и частотность значимых слов.
        var words = Tokenize(text);
        var frequencies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var word in words)
        {
            if (word.Length < 3 || StopWords.Contains(word))
            {
                continue;
            }
            frequencies[word] = frequencies.TryGetValue(word, out var c) ? c + 1 : 1;
        }

        // 2. Тема — самые частотные слова.
        var topWords = frequencies
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .Select(kv => kv.Key)
            .ToArray();

        string theme = topWords.Length == 0
            ? string.Empty
            : string.Join(" ", topWords.Select(Capitalize));

        // 3. Краткое содержание — предложения с наибольшей «плотностью» топ-слов.
        string summary = BuildSummary(text, topWords);

        return new TextAnalysisResult(theme, summary);
    }

    private static string BuildSummary(string text, IReadOnlyList<string> topWords)
    {
        var keySet = new HashSet<string>(topWords, StringComparer.OrdinalIgnoreCase);
        if (keySet.Count == 0)
        {
            return string.Empty;
        }

        var sentences = text
            .Split(SentenceSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length >= 20)
            .ToArray();

        if (sentences.Length == 0)
        {
            return string.Empty;
        }

        var scored = sentences
            .Select((s, i) => new
            {
                Index = i,
                Sentence = s,
                Score = Tokenize(s).Count(keySet.Contains)
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Index)
            .Take(3)
            .OrderBy(x => x.Index)
            .Select(x => CleanColumn(x.Sentence));

        var summary = string.Join(" ", scored);
        return Truncate(summary, 500);
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        var span = text.AsSpan();
        var result = new List<string>();
        var current = new System.Text.StringBuilder();

        foreach (var ch in span)
        {
            if (char.IsLetterOrDigit(ch) || ch == '\'')
            {
                if (current.Length == 0 && ch == '\'')
                {
                    continue; // пропускаем начальную кавычку
                }
                current.Append(char.ToLowerInvariant(ch));
            }
            else if (current.Length > 0)
            {
                result.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }

        return result;
    }

    private static string Capitalize(string word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return word;
        }
        return char.ToUpperInvariant(word[0]) + word[1..];
    }

    private static string CleanColumn(string s)
    {
        // Убираем переносы строк/табуляции, чтобы не портить вид таблицы.
        foreach (var c in new[] { '\r', '\n', '\t' })
        {
            s = s.Replace(c.ToString(), " ");
        }
        return string.Join(" ", s.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string Truncate(string s, int max)
    {
        return s.Length <= max ? s : s[..max].TrimEnd() + "…";
    }
}