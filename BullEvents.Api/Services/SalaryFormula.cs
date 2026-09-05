using System.Globalization;

namespace BullEvents.Api.Services;

/// <summary>
/// Evaluates a salary component's formula.
///
/// A recursive-descent parser over a deliberately small grammar: numbers,
/// component abbreviations, the four operators, brackets, and three functions.
/// Nothing else is accepted.
///
/// It is written by hand rather than reached for from a library because the
/// input is a string a payroll clerk types into a form, and payroll runs on the
/// server. A general expression evaluator that can call methods, or a
/// scripting engine, is a remote code execution hole with a text box in front
/// of it. This one can only ever produce a number.
///
/// <code>
///   BASIC * 0.4
///   min(BASIC * 0.5, 15000)
///   round((BASIC + DA) * 0.12)
/// </code>
/// </summary>
public static class SalaryFormula
{
    /// <summary>Thrown when a formula cannot be parsed or names something unknown.</summary>
    public sealed class FormulaException(string message) : Exception(message);

    /// <summary>
    /// Work out what a formula comes to.
    ///
    /// <paramref name="values"/> maps an abbreviation to its amount. Names are
    /// matched without regard to case, because nobody types BASIC consistently.
    /// </summary>
    public static decimal Evaluate(string formula, IReadOnlyDictionary<string, decimal> values)
    {
        if (string.IsNullOrWhiteSpace(formula)) return 0m;

        var parser = new Parser(formula, values);
        var result = parser.ParseExpression();
        parser.ExpectEnd();
        return result;
    }

    /// <summary>
    /// Check a formula without running it, for the form that accepts one.
    ///
    /// Returns the problem, or null when it parses. Evaluated against zeroes,
    /// so it catches a misspelt component and a stray bracket but not a
    /// division by a component that happens to be zero this month.
    /// </summary>
    public static string? Validate(string? formula, IEnumerable<string> knownNames)
    {
        if (string.IsNullOrWhiteSpace(formula)) return null;

        var values = knownNames.ToDictionary(n => n, _ => 0m, StringComparer.OrdinalIgnoreCase);
        try
        {
            Evaluate(formula, values);
            return null;
        }
        catch (FormulaException e)
        {
            return e.Message;
        }
    }

    /// <summary>The abbreviations a formula refers to, so dependencies can be ordered.</summary>
    public static IReadOnlyList<string> ReferencedNames(string? formula)
    {
        if (string.IsNullOrWhiteSpace(formula)) return [];

        var names = new List<string>();
        var index = 0;
        while (index < formula.Length)
        {
            var c = formula[index];
            if (char.IsLetter(c) || c == '_')
            {
                var start = index;
                while (index < formula.Length
                    && (char.IsLetterOrDigit(formula[index]) || formula[index] == '_'))
                {
                    index++;
                }

                var name = formula[start..index];
                // A name followed by a bracket is a function call, not a component.
                var next = index;
                while (next < formula.Length && char.IsWhiteSpace(formula[next])) next++;
                if (next < formula.Length && formula[next] == '(') continue;

                if (!names.Contains(name, StringComparer.OrdinalIgnoreCase)) names.Add(name);
            }
            else
            {
                index++;
            }
        }

        return names;
    }

    /// <summary>
    /// expression := term (('+' | '-') term)*
    /// term       := factor (('*' | '/') factor)*
    /// factor     := ['-'] primary
    /// primary    := number | name | function '(' args ')' | '(' expression ')'
    /// </summary>
    private sealed class Parser(string text, IReadOnlyDictionary<string, decimal> values)
    {
        private int _position;

        public decimal ParseExpression()
        {
            var left = ParseTerm();

            while (true)
            {
                SkipSpace();
                if (Peek() == '+') { _position++; left += ParseTerm(); }
                else if (Peek() == '-') { _position++; left -= ParseTerm(); }
                else return left;
            }
        }

        private decimal ParseTerm()
        {
            var left = ParseFactor();

            while (true)
            {
                SkipSpace();
                if (Peek() == '*')
                {
                    _position++;
                    left *= ParseFactor();
                }
                else if (Peek() == '/')
                {
                    _position++;
                    var divisor = ParseFactor();
                    // A component that is zero this month is ordinary — an
                    // employee on unpaid leave, an allowance not yet started —
                    // so this must not throw the whole run over.
                    left = divisor == 0m ? 0m : left / divisor;
                }
                else
                {
                    return left;
                }
            }
        }

        private decimal ParseFactor()
        {
            SkipSpace();
            if (Peek() == '-') { _position++; return -ParseFactor(); }
            if (Peek() == '+') { _position++; return ParseFactor(); }
            return ParsePrimary();
        }

        private decimal ParsePrimary()
        {
            SkipSpace();

            if (_position >= text.Length)
                throw new FormulaException("The formula ends where a value was expected.");

            var c = text[_position];

            if (c == '(')
            {
                _position++;
                var inner = ParseExpression();
                SkipSpace();
                if (Peek() != ')') throw new FormulaException("A bracket is not closed.");
                _position++;
                return inner;
            }

            if (char.IsDigit(c) || c == '.')
            {
                var start = _position;
                while (_position < text.Length
                    && (char.IsDigit(text[_position]) || text[_position] == '.'))
                {
                    _position++;
                }

                var literal = text[start.._position];
                if (!decimal.TryParse(literal, NumberStyles.Number,
                        CultureInfo.InvariantCulture, out var number))
                {
                    throw new FormulaException($"'{literal}' is not a number.");
                }

                return number;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var start = _position;
                while (_position < text.Length
                    && (char.IsLetterOrDigit(text[_position]) || text[_position] == '_'))
                {
                    _position++;
                }

                var name = text[start.._position];

                SkipSpace();
                if (Peek() == '(') return ParseFunction(name);

                if (values.TryGetValue(name, out var value)) return value;

                throw new FormulaException(
                    $"'{name}' is not a component on this structure. "
                    + "Use a component's abbreviation, or BASE for the assignment's base figure.");
            }

            throw new FormulaException($"'{c}' cannot appear in a formula.");
        }

        private decimal ParseFunction(string name)
        {
            _position++;   // past the '('
            var args = new List<decimal>();

            SkipSpace();
            if (Peek() != ')')
            {
                while (true)
                {
                    args.Add(ParseExpression());
                    SkipSpace();
                    if (Peek() == ',') { _position++; continue; }
                    break;
                }
            }

            SkipSpace();
            if (Peek() != ')') throw new FormulaException($"{name}( is not closed.");
            _position++;

            return name.ToLowerInvariant() switch
            {
                "min" => args.Count >= 1
                    ? args.Min()
                    : throw new FormulaException("min() needs at least one value."),
                "max" => args.Count >= 1
                    ? args.Max()
                    : throw new FormulaException("max() needs at least one value."),
                "round" => args.Count switch
                {
                    1 => Math.Round(args[0], 0, MidpointRounding.AwayFromZero),
                    2 => Math.Round(args[0], (int)Math.Clamp(args[1], 0, 6),
                            MidpointRounding.AwayFromZero),
                    _ => throw new FormulaException("round() takes a value and optionally a scale."),
                },
                _ => throw new FormulaException(
                    $"'{name}' is not a function. Only min, max and round are available."),
            };
        }

        public void ExpectEnd()
        {
            SkipSpace();
            if (_position < text.Length)
            {
                throw new FormulaException(
                    $"'{text[_position..]}' is left over at the end of the formula.");
            }
        }

        private char Peek() => _position < text.Length ? text[_position] : '\0';

        private void SkipSpace()
        {
            while (_position < text.Length && char.IsWhiteSpace(text[_position])) _position++;
        }
    }
}
