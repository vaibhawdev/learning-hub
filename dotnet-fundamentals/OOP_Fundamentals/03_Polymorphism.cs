// See 03_Polymorphism.md for the full guide, vtable internals, and decision tables.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OOP_Fundamentals.PolymorphismPrinciple
{
    // ── Compile-time polymorphism ──────────────────────────────────────────────
    public sealed class OrderCalculator
    {
        public decimal Calculate(int qty, decimal unitPrice)
            => qty * unitPrice;

        public decimal Calculate(int qty, decimal unitPrice, decimal discountPercent)
        {
            if (discountPercent is < 0 or > 1)
                throw new ArgumentOutOfRangeException(nameof(discountPercent), "Must be 0–1");
            return qty * unitPrice * (1 - discountPercent);
        }

        public decimal Calculate(int qty, decimal unitPrice, string couponCode)
        {
            decimal discount = couponCode switch
            {
                "SAVE10" => 0.10m,
                "SAVE20" => 0.20m,
                "VIP"    => 0.30m,
                _        => 0m
            };
            return qty * unitPrice * (1 - discount);
        }

        public T Parse<T>(string raw) where T : IParsable<T> => T.Parse(raw, null);
    }

    namespace BadCode
    {
        public enum PaymentType { CreditCard, PayPal, BankTransfer, Crypto }

        // Every new type → modify this switch. Every feature → copy this switch.
        public class PaymentProcessor_Switch
        {
            public string Process(PaymentType type, decimal amount, string reference)
            {
                return type switch
                {
                    PaymentType.CreditCard   => $"[CC] Charged {amount:C} to {reference}",
                    PaymentType.PayPal       => $"[PP] Sent {amount:C} via PayPal",
                    PaymentType.BankTransfer => $"[BT] Transferred {amount:C}",
                    PaymentType.Crypto       => $"[CR] Crypto {amount} {reference}",
                    _                        => throw new NotSupportedException($"Unknown: {type}")
                };
            }
        }
    }

    namespace GoodCode
    {
        public record PaymentRequest(decimal Amount, string Currency, string Reference);
        public record PaymentResult(bool Success, string TransactionId, string Message);
        public record NotificationResult(bool Success, string Message)
        {
            public static NotificationResult Failure(string m) => new(false, m);
            public static NotificationResult Ok(string m)      => new(true, m);
        }

        public interface IPaymentMethod
        {
            string ProviderName { get; }
            bool   Supports(string currency);
            PaymentResult Process(PaymentRequest request);
            PaymentResult Refund(string transactionId, decimal amount);
        }

        public sealed class CreditCardPayment : IPaymentMethod
        {
            private readonly string _apiKey;
            public CreditCardPayment(string apiKey) => _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));

            public string ProviderName => "Stripe";
            public bool   Supports(string currency) => currency is "USD" or "EUR" or "GBP";

            public PaymentResult Process(PaymentRequest req)
            {
                var txId = $"ch_{Guid.NewGuid():N}"[..15];
                return new PaymentResult(true, txId, $"Charged {req.Amount:C} via Stripe");
            }

            public PaymentResult Refund(string txId, decimal amount)
                => new(true, $"re_{txId}", $"Refunded {amount:C}");
        }

        public sealed class PayPalPayment : IPaymentMethod
        {
            public string ProviderName => "PayPal";
            public bool   Supports(string currency) => currency is "USD" or "EUR" or "AUD" or "CAD";

            public PaymentResult Process(PaymentRequest req)
            {
                var txId = $"PAY-{Guid.NewGuid():N}"[..20];
                return new PaymentResult(true, txId, $"PayPal payment of {req.Amount:C}");
            }

            public PaymentResult Refund(string txId, decimal amount)
                => new(true, $"REF-{txId}", $"PayPal refund {amount:C}");
        }

        public sealed class CryptoPayment : IPaymentMethod
        {
            public string ProviderName => "Coinbase Commerce";
            public bool   Supports(string currency) => currency is "BTC" or "ETH" or "USDC";

            public PaymentResult Process(PaymentRequest req)
                => new(true, Guid.NewGuid().ToString("N"), $"Crypto charge {req.Amount} {req.Currency}");

            // Returns failure rather than throwing — callers don't break
            public PaymentResult Refund(string txId, decimal amount)
                => new(false, string.Empty, "Crypto transactions are irreversible. Contact support.");
        }

        public sealed class PaymentProcessor
        {
            private readonly IReadOnlyList<IPaymentMethod> _methods;
            public PaymentProcessor(IEnumerable<IPaymentMethod> methods)
                => _methods = new List<IPaymentMethod>(methods);

            public PaymentResult Process(PaymentRequest request)
            {
                var method = _methods.FirstOrDefault(m => m.Supports(request.Currency))
                    ?? throw new InvalidOperationException($"No provider supports '{request.Currency}'");
                return method.Process(request);
            }
        }

        // ── Abstract class: shared state + template method ─────────────────────
        public abstract class Notification
        {
            protected readonly string Sender;
            protected readonly DateTimeOffset CreatedAt;

            protected Notification(string sender)
            {
                Sender    = sender ?? throw new ArgumentNullException(nameof(sender));
                CreatedAt = DateTimeOffset.UtcNow;
            }

            // Sealed template — algorithm is fixed
            public sealed NotificationResult Send(string recipient, string subject, string body)
            {
                if (!Validate(recipient)) return NotificationResult.Failure($"Invalid recipient: {recipient}");
                return Deliver(recipient, subject, FormatBody(body));
            }

            protected abstract bool               Validate(string recipient);
            protected abstract string             FormatBody(string raw);
            protected abstract NotificationResult Deliver(string to, string subject, string body);
        }

        public sealed class EmailNotification : Notification
        {
            private readonly string _smtpHost;
            public EmailNotification(string sender, string smtpHost) : base(sender) => _smtpHost = smtpHost;

            protected override bool   Validate(string r)       => r.Contains('@') && r.Contains('.');
            protected override string FormatBody(string raw)   => $"<html><body>{raw}</body></html>";
            protected override NotificationResult Deliver(string to, string subject, string body)
            {
                Console.WriteLine($"[EMAIL] From:{Sender} To:{to} via {_smtpHost}");
                return NotificationResult.Ok($"Email sent to {to}");
            }
        }

        public sealed class SmsNotification : Notification
        {
            public SmsNotification(string sender) : base(sender) { }

            protected override bool   Validate(string r)       => r.StartsWith('+') && r.Length >= 10;
            protected override string FormatBody(string raw)   => raw.Length > 160 ? raw[..157] + "..." : raw;
            protected override NotificationResult Deliver(string to, string subject, string body)
            {
                Console.WriteLine($"[SMS] To:{to} '{body}'");
                return NotificationResult.Ok($"SMS sent to {to}");
            }
        }
    }

    namespace AdvancedConcepts
    {
        // ── vtable dispatch — sealed enables devirtualization ─────────────────
        public abstract class Shape
        {
            public abstract double Area();
            public abstract double Perimeter();
            public string Describe() => $"{GetType().Name}: Area={Area():F2}, Perimeter={Perimeter():F2}";
        }

        public sealed class Circle : Shape
        {
            private readonly double _r;
            public Circle(double r) { if (r <= 0) throw new ArgumentException("Positive radius required"); _r = r; }
            public sealed override double Area()      => Math.PI * _r * _r;
            public sealed override double Perimeter() => 2 * Math.PI * _r;
        }

        public sealed class Rectangle : Shape
        {
            private readonly double _w, _h;
            public Rectangle(double w, double h) { _w = w; _h = h; }
            public sealed override double Area()      => _w * _h;
            public sealed override double Perimeter() => 2 * (_w + _h);
        }

        public sealed class Triangle : Shape
        {
            private readonly double _a, _b, _c;
            public Triangle(double a, double b, double c)
            {
                if (a + b <= c || a + c <= b || b + c <= a)
                    throw new ArgumentException("Triangle inequality violated");
                (_a, _b, _c) = (a, b, c);
            }
            public sealed override double Area()
            {
                double s = (_a + _b + _c) / 2;
                return Math.Sqrt(s * (s - _a) * (s - _b) * (s - _c));
            }
            public sealed override double Perimeter() => _a + _b + _c;
        }

        // ── Pattern matching on closed record sets ─────────────────────────────
        public abstract record ShapeRecord;
        public sealed record CircleRecord(double Radius)              : ShapeRecord;
        public sealed record RectangleRecord(double Width, double Height) : ShapeRecord;
        public sealed record TriangleRecord(double Base, double Height)   : ShapeRecord;

        public static class ShapeAreaCalculator
        {
            public static double Calculate(ShapeRecord s) => s switch
            {
                CircleRecord c    => Math.PI * c.Radius * c.Radius,
                RectangleRecord r => r.Width * r.Height,
                TriangleRecord t  => 0.5 * t.Base * t.Height,
                _                 => throw new ArgumentException($"Unknown: {s.GetType().Name}")
            };
        }

        // ── Covariant return type (C# 9) ───────────────────────────────────────
        public abstract class DocumentBuilder
        {
            public abstract DocumentBuilder WithTitle(string title);
            public abstract string Build();
        }

        public sealed class PdfDocumentBuilder : DocumentBuilder
        {
            private string _title = string.Empty;
            private bool   _compressed;

            public override PdfDocumentBuilder WithTitle(string title) { _title = title; return this; }

            public PdfDocumentBuilder WithCompression(bool compress) { _compressed = compress; return this; }

            public override string Build() => $"[PDF] Title={_title}, Compressed={_compressed}";
        }

        // ── C# 8 default interface methods ─────────────────────────────────────
        public interface ILogger
        {
            void Log(string level, string message);

            // Defaults let existing loggers skip implementation
            void LogInfo(string m)    => Log("INFO",    m);
            void LogWarning(string m) => Log("WARNING", m);
            void LogError(string m)   => Log("ERROR",   m);
        }

        public sealed class ConsoleLogger : ILogger
        {
            public void Log(string level, string message)
                => Console.WriteLine($"[{DateTimeOffset.UtcNow:HH:mm:ss}] [{level}] {message}");
        }
    }

    public static class PolymorphismDemo
    {
        public static void Run()
        {
            Console.WriteLine("=== Compile-time overloading ===");
            var calc = new OrderCalculator();
            Console.WriteLine(calc.Calculate(5, 10m));
            Console.WriteLine(calc.Calculate(5, 10m, 0.15m));
            Console.WriteLine(calc.Calculate(5, 10m, "SAVE20"));

            Console.WriteLine("\n=== Runtime polymorphism — payments ===");
            var processor = new GoodCode.PaymentProcessor(new GoodCode.IPaymentMethod[]
            {
                new GoodCode.CreditCardPayment("sk_test"),
                new GoodCode.PayPalPayment(),
                new GoodCode.CryptoPayment()
            });
            Console.WriteLine(processor.Process(new GoodCode.PaymentRequest(99.99m, "USD", "ord-1")).Message);
            Console.WriteLine(processor.Process(new GoodCode.PaymentRequest(0.001m, "BTC", "ord-2")).Message);

            Console.WriteLine("\n=== Abstract class template method ===");
            GoodCode.Notification email = new GoodCode.EmailNotification("noreply@co.com", "smtp.co.com");
            Console.WriteLine(email.Send("user@example.com", "Hi", "Hello").Message);

            Console.WriteLine("\n=== Pattern matching (closed set) ===");
            AdvancedConcepts.ShapeRecord[] shapes = { new AdvancedConcepts.CircleRecord(5), new AdvancedConcepts.RectangleRecord(4, 6) };
            foreach (var s in shapes)
                Console.WriteLine($"{s.GetType().Name}: {AdvancedConcepts.ShapeAreaCalculator.Calculate(s):F2}");

            Console.WriteLine("\n=== Virtual dispatch (abstract class) ===");
            AdvancedConcepts.Shape[] vtable = { new AdvancedConcepts.Circle(5), new AdvancedConcepts.Rectangle(4, 6) };
            foreach (var s in vtable) Console.WriteLine(s.Describe());
        }
    }
}
