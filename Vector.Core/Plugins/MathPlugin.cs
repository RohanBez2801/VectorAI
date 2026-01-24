using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace Vector.Core.Plugins;

public class MathPlugin
{
    // --- 1. EXISTING SCALAR MATH (Preserved) ---
    [KernelFunction, Description("Evaluates mathematical expressions including trigonometry, powers, and logs.")]
    public string Calculate(
        [Description("The mathematical expression (e.g., 'Sqrt(16) * sin(pi/2) + 2^3')")] string input)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(input)) return "0";
            
            // Normalize input
            var cleanInput = input.ToLowerInvariant().Replace(" ", "");
            
            var parser = new MathParser(cleanInput);
            double result = parser.Parse();
            
            return result.ToString(CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    // --- 2. NEW: VECTOR ALGEBRA (Calculus III Essentials) ---
    [KernelFunction, Description("Performs Vector operations (Dot, Cross, Magnitude, Angle, Distance). inputs should be comma-separated strings like '1,0,0'.")]
    public string VectorMath(
        [Description("Operation: 'dot', 'cross', 'add', 'sub', 'mag', 'angle', 'dist'")] string operation,
        [Description("Vector A (e.g., '1,2,3')")] string vectorA,
        [Description("Vector B (e.g., '4,5,6') - Optional for 'mag'")] string vectorB = "")
    {
        try
        {
            var vA = ParseVector(vectorA);
            var vB = string.IsNullOrWhiteSpace(vectorB) ? new double[0] : ParseVector(vectorB);

            switch (operation.ToLowerInvariant())
            {
                case "add":
                    if (vA.Length != vB.Length) return "Error: Dimension mismatch";
                    return VectorToString(vA.Zip(vB, (a, b) => a + b).ToArray());
                
                case "sub":
                    if (vA.Length != vB.Length) return "Error: Dimension mismatch";
                    return VectorToString(vA.Zip(vB, (a, b) => a - b).ToArray());

                case "dot": // Dot Product (Scalar)
                    if (vA.Length != vB.Length) return "Error: Dimension mismatch";
                    double dot = vA.Zip(vB, (a, b) => a * b).Sum();
                    return dot.ToString(CultureInfo.InvariantCulture);

                case "cross": // Cross Product (3D only)
                    if (vA.Length != 3 || vB.Length != 3) return "Error: Cross product requires 3D vectors";
                    double x = vA[1] * vB[2] - vA[2] * vB[1];
                    double y = vA[2] * vB[0] - vA[0] * vB[2];
                    double z = vA[0] * vB[1] - vA[1] * vB[0];
                    return $"{x},{y},{z}";

                case "mag": // Magnitude (Length)
                    return Math.Sqrt(vA.Sum(x => x * x)).ToString(CultureInfo.InvariantCulture);

                case "dist": // Euclidean Distance
                    if (vA.Length != vB.Length) return "Error: Dimension mismatch";
                    double distSq = vA.Zip(vB, (a, b) => Math.Pow(a - b, 2)).Sum();
                    return Math.Sqrt(distSq).ToString(CultureInfo.InvariantCulture);

                case "angle": // Angle between vectors (Radians)
                    if (vA.Length != vB.Length) return "Error: Dimension mismatch";
                    double dotProd = vA.Zip(vB, (a, b) => a * b).Sum();
                    double magA = Math.Sqrt(vA.Sum(k => k * k));
                    double magB = Math.Sqrt(vB.Sum(k => k * k));
                    if (magA == 0 || magB == 0) return "0";
                    double theta = Math.Acos(Math.Clamp(dotProd / (magA * magB), -1.0, 1.0));
                    return theta.ToString(CultureInfo.InvariantCulture);

                default:
                    return $"Error: Unknown vector operation '{operation}'";
            }
        }
        catch (Exception ex)
        {
            return $"Vector Error: {ex.Message}";
        }
    }

    // --- 3. NEW: CALCULUS ENGINE (Numerical Analysis) ---
    [KernelFunction, Description("Performs numerical calculus operations (Integration, Differentiation).")]
    public string Calculus(
        [Description("Operation: 'integrate' (Definite Integral) or 'derive' (Derivative at point)")] string operation,
        [Description("The mathematical function of x (e.g., 'x^2 + sin(x)')")] string expression,
        [Description("For Integrate: Start limit. For Derive: The point x to evaluate.")] double param1,
        [Description("For Integrate: End limit. For Derive: Unused.")] double param2 = 0)
    {
        try
        {
            string funcStr = expression.ToLowerInvariant().Replace(" ", "");

            if (operation.ToLower() == "integrate")
            {
                // Simpson's Rule for Definite Integral
                double a = param1;
                double b = param2;
                int n = 1000; // Steps (higher = more precision)
                double h = (b - a) / n;
                
                double sum = EvaluateFunction(funcStr, a) + EvaluateFunction(funcStr, b);
                
                for (int i = 1; i < n; i++)
                {
                    double x = a + i * h;
                    sum += EvaluateFunction(funcStr, x) * ((i % 2 == 0) ? 2 : 4);
                }
                
                double integral = (sum * h) / 3.0;
                return integral.ToString(CultureInfo.InvariantCulture);
            }
            else if (operation.ToLower() == "derive")
            {
                // Central Difference Method for Numerical Derivative
                double x = param1;
                double h = 1e-5; // Small step
                double fPlus = EvaluateFunction(funcStr, x + h);
                double fMinus = EvaluateFunction(funcStr, x - h);
                double derivative = (fPlus - fMinus) / (2 * h);
                return derivative.ToString(CultureInfo.InvariantCulture);
            }

            return "Error: Unknown calculus operation";
        }
        catch (Exception ex)
        {
            return $"Calculus Error: {ex.Message}";
        }
    }

    // Helper: Evaluates f(x) for a specific value of x
    private double EvaluateFunction(string expression, double xValue)
    {
        // Pass the variable 'x' to the parser
        var vars = new Dictionary<string, double> { { "x", xValue } };
        var parser = new MathParser(expression, vars);
        return parser.Parse();
    }

    private double[] ParseVector(string vec)
    {
        return vec.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                  .Select(s => double.Parse(s, CultureInfo.InvariantCulture))
                  .ToArray();
    }

    private string VectorToString(double[] v)
    {
        return string.Join(",", v.Select(d => d.ToString(CultureInfo.InvariantCulture)));
    }

    // --- UPDATED PARSER (Supports Variables now) ---
    private class MathParser
    {
        private readonly string _text;
        private readonly Dictionary<string, double> _variables; // NEW: Context for variables
        private int _pos;
        private int _ch;

        public MathParser(string text, Dictionary<string, double>? variables = null)
        {
            _text = text;
            _variables = variables ?? new Dictionary<string, double>();
            _pos = -1;
            NextChar();
        }

        private void NextChar()
        {
            _ch = (++_pos < _text.Length) ? _text[_pos] : -1;
        }

        private bool Eat(int charToEat)
        {
            while (_ch == ' ') NextChar();
            if (_ch == charToEat)
            {
                NextChar();
                return true;
            }
            return false;
        }

        public double Parse()
        {
            double x = ParseExpression();
            if (_pos < _text.Length) throw new Exception($"Unexpected character: {(char)_ch}");
            return x;
        }

        // Expression = Term { (+|-) Term }
        private double ParseExpression()
        {
            double x = ParseTerm();
            while (true)
            {
                if (Eat('+')) x += ParseTerm();
                else if (Eat('-')) x -= ParseTerm();
                else return x;
            }
        }

        // Term = Factor { (*|/|%) Factor }
        private double ParseTerm()
        {
            double x = ParseFactor();
            while (true)
            {
                if (Eat('*')) x *= ParseFactor();
                else if (Eat('/')) x /= ParseFactor();
                else if (Eat('%')) x %= ParseFactor();
                else return x;
            }
        }

        // Factor = (+|-) Factor | Base ^ Factor | Function | ( Expression ) | Number | Variable
        private double ParseFactor()
        {
            if (Eat('+')) return ParseFactor();
            if (Eat('-')) return -ParseFactor();

            double x;
            int startPos = _pos;

            if (Eat('('))
            {
                x = ParseExpression();
                Eat(')');
            }
            else if ((_ch >= '0' && _ch <= '9') || _ch == '.')
            {
                // Number parsing
                while ((_ch >= '0' && _ch <= '9') || _ch == '.') NextChar();
                var numStr = _text.Substring(startPos, _pos - startPos);
                x = double.Parse(numStr, CultureInfo.InvariantCulture);
            }
            else if (_ch >= 'a' && _ch <= 'z')
            {
                // Functions, Constants, and Variables
                while (_ch >= 'a' && _ch <= 'z') NextChar();
                string func = _text.Substring(startPos, _pos - startPos);
                
                // 1. Check Constants
                if (func == "pi") x = Math.PI;
                else if (func == "e") x = Math.E;
                // 2. Check Variables (e.g., 'x' for calculus)
                else if (_variables.ContainsKey(func)) x = _variables[func];
                // 3. Check Functions
                else
                {
                    if (!Eat('(')) throw new Exception($"Function '{func}' expects '(' or is unknown variable");
                    double arg = ParseExpression();
                    Eat(')');

                    x = func switch
                    {
                        "sqrt" => Math.Sqrt(arg),
                        "sin"  => Math.Sin(arg),
                        "cos"  => Math.Cos(arg),
                        "tan"  => Math.Tan(arg),
                        "asin" => Math.Asin(arg),
                        "acos" => Math.Acos(arg),
                        "atan" => Math.Atan(arg),
                        "abs"  => Math.Abs(arg),
                        "log"  => Math.Log10(arg),
                        "ln"   => Math.Log(arg),
                        "floor"=> Math.Floor(arg),
                        "ceil" => Math.Ceiling(arg),
                        "round"=> Math.Round(arg),
                        "exp"  => Math.Exp(arg), // Added exp(x) for Calculus
                        _ => throw new Exception($"Unknown function: {func}")
                    };
                }
            }
            else
            {
                throw new Exception($"Unexpected character: {(char)_ch}");
            }

            // Handle Power (^)
            if (Eat('^')) x = Math.Pow(x, ParseFactor());

            return x;
        }
    }
}