// See 01_Single_Responsibility.md for full guide, actor concept, and decision tables.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SOLID.SingleResponsibility
{
    namespace BadCode
    {
        // God class: Security, DBA, Marketing, and Finance all own this one file.
        public class UserService_God
        {
            private readonly string _connectionString;
            private readonly string _smtpServer;

            public UserService_God(string connectionString, string smtpServer)
            {
                _connectionString = connectionString;
                _smtpServer       = smtpServer;
            }

            // Security team's concern
            public string HashPassword(string raw)
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
                return Convert.ToHexString(bytes);
            }

            public bool ValidatePassword(string raw, string hash) => HashPassword(raw) == hash;

            // DBA's concern — wants to swap ADO.NET for Dapper
            public User GetUserById(int id)
            {
                // real impl: SQL query here
                return new User { Id = id, Name = "Alice", Email = "alice@example.com" };
            }

            public void SaveUser(User user)
            {
                // real impl: INSERT/UPDATE SQL
            }

            // Marketing's concern — changes template quarterly
            public void SendWelcomeEmail(User user)
            {
                Console.WriteLine($"[SMTP:{_smtpServer}] Welcome email → {user.Email}");
            }

            public void SendPasswordResetEmail(User user, string token)
            {
                Console.WriteLine($"[SMTP:{_smtpServer}] Reset email → {user.Email} token:{token}");
            }

            // Finance's concern — needs new line-item format
            public byte[] GenerateInvoicePdf(Order order)
                => throw new NotImplementedException("PDF generation — 200 lines, next to HashPassword 😢");

            public class User  { public int Id { get; set; } public string Name { get; set; } = ""; public string Email { get; set; } = ""; }
            public class Order { public int Id { get; set; } public decimal Total { get; set; } }
        }

        // Infrastructure concern leaking into domain entity
        [System.ComponentModel.DataAnnotations.Schema.Table("Orders")]
        public class Order_Polluted
        {
            [System.ComponentModel.DataAnnotations.Key]
            public int Id { get; set; }

            public virtual ICollection<OrderLine_Polluted> Lines { get; set; } = new List<OrderLine_Polluted>();

            public void AddLine(OrderLine_Polluted line)
            {
                Lines.Add(line);
                // EF tracking + audit logging both live in the domain entity
                Console.WriteLine($"[AUDIT] Line added to order {Id} at {DateTime.UtcNow}");
            }
        }

        public class OrderLine_Polluted { public int Id { get; set; } }
    }

    namespace GoodCode
    {
        // ── Domain models ──────────────────────────────────────────────────────
        public sealed class User
        {
            public Guid           Id           { get; set; }
            public string         Name         { get; set; } = string.Empty;
            public string         Email        { get; set; } = string.Empty;
            public string         PasswordHash { get; set; } = string.Empty;
            public DateTimeOffset CreatedAt    { get; set; }
        }

        public sealed class Order
        {
            private readonly List<OrderLine> _lines = new();
            public Guid   Id         { get; } = Guid.NewGuid();
            public string CustomerId { get; }
            public IReadOnlyList<OrderLine> Lines => _lines;
            public decimal Total => _lines.Sum(l => l.Subtotal);

            public Order(string customerId)
            {
                if (string.IsNullOrWhiteSpace(customerId)) throw new ArgumentException("CustomerId required");
                CustomerId = customerId;
            }

            public void AddLine(OrderLine line)
            {
                ArgumentNullException.ThrowIfNull(line);
                _lines.Add(line);
            }
        }

        public sealed class OrderLine
        {
            public string  ProductName { get; }
            public int     Quantity    { get; }
            public decimal UnitPrice   { get; }
            public decimal Subtotal    => Quantity * UnitPrice;

            public OrderLine(string name, int qty, decimal price)
            {
                ProductName = name;
                Quantity    = qty > 0 ? qty : throw new ArgumentException("Qty must be positive");
                UnitPrice   = price >= 0 ? price : throw new ArgumentException("Price cannot be negative");
            }
        }

        // ── Security team owns this ─────────────────────────────────────────────
        public interface IPasswordHasher
        {
            string Hash(string raw);
            bool   Verify(string raw, string hash);
        }

        public sealed class Pbkdf2PasswordHasher : IPasswordHasher
        {
            private const int Iterations = 100_000;

            public string Hash(string raw)
            {
                var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
                var hash = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
                    raw, salt, Iterations,
                    System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
                return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
            }

            public bool Verify(string raw, string stored)
            {
                var parts = stored.Split(':');
                if (parts.Length != 2) return false;
                var salt     = Convert.FromBase64String(parts[0]);
                var expected = Convert.FromBase64String(parts[1]);
                var actual   = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
                    raw, salt, Iterations,
                    System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
                return actual.SequenceEqual(expected);
            }
        }

        // ── DBA owns this ───────────────────────────────────────────────────────
        public interface IUserRepository
        {
            Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
            Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
            Task        SaveAsync(User user, CancellationToken ct = default);
        }

        public sealed class InMemoryUserRepository : IUserRepository
        {
            private readonly Dictionary<Guid, User>   _byId    = new();
            private readonly Dictionary<string, User> _byEmail = new();

            public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
                => Task.FromResult(_byId.TryGetValue(id, out var u) ? u : null);

            public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
                => Task.FromResult(_byEmail.TryGetValue(email, out var u) ? u : null);

            public Task SaveAsync(User user, CancellationToken ct = default)
            {
                _byId[user.Id]      = user;
                _byEmail[user.Email] = user;
                return Task.CompletedTask;
            }
        }

        // ── Marketing team owns this ────────────────────────────────────────────
        public interface IUserEmailService
        {
            Task SendWelcomeAsync(User user, CancellationToken ct = default);
            Task SendPasswordResetAsync(User user, string token, CancellationToken ct = default);
        }

        public sealed class ConsoleUserEmailService : IUserEmailService
        {
            public Task SendWelcomeAsync(User user, CancellationToken ct = default)
            {
                Console.WriteLine($"[EMAIL] Welcome → {user.Email}");
                return Task.CompletedTask;
            }

            public Task SendPasswordResetAsync(User user, string token, CancellationToken ct = default)
            {
                Console.WriteLine($"[EMAIL] Reset → {user.Email} token:{token}");
                return Task.CompletedTask;
            }
        }

        // ── Validation — QA/Security team owns this ─────────────────────────────
        public record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
        {
            public static ValidationResult Ok()                        => new(true,  Array.Empty<string>());
            public static ValidationResult Failure(List<string> errs)  => new(false, errs);
        }

        public sealed class UserRegistrationValidator
        {
            public ValidationResult Validate(string name, string email, string password)
            {
                var errors = new List<string>();
                if (string.IsNullOrWhiteSpace(name))  errors.Add("Name is required");
                if (!email.Contains('@'))             errors.Add("Invalid email format");
                if (password.Length < 8)             errors.Add("Password must be at least 8 characters");
                if (!password.Any(char.IsDigit))     errors.Add("Password must contain a digit");
                if (!password.Any(char.IsUpper))     errors.Add("Password must contain an uppercase letter");
                return errors.Count == 0 ? ValidationResult.Ok() : ValidationResult.Failure(errors);
            }
        }

        // ── Application orchestrator — thin, no business logic ──────────────────
        public record RegistrationResult(bool Success, Guid? UserId, string? Error)
        {
            public static RegistrationResult Ok(Guid id)       => new(true, id, null);
            public static RegistrationResult Failure(string e) => new(false, null, e);
        }

        public sealed class UserRegistrationService
        {
            private readonly IUserRepository          _users;
            private readonly IPasswordHasher          _hasher;
            private readonly IUserEmailService        _email;
            private readonly UserRegistrationValidator _validator;

            public UserRegistrationService(
                IUserRepository users, IPasswordHasher hasher,
                IUserEmailService email, UserRegistrationValidator validator)
            {
                _users     = users     ?? throw new ArgumentNullException(nameof(users));
                _hasher    = hasher    ?? throw new ArgumentNullException(nameof(hasher));
                _email     = email     ?? throw new ArgumentNullException(nameof(email));
                _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            }

            public async Task<RegistrationResult> RegisterAsync(
                string name, string email, string password, CancellationToken ct = default)
            {
                var validation = _validator.Validate(name, email, password);
                if (!validation.IsValid)
                    return RegistrationResult.Failure(string.Join("; ", validation.Errors));

                if (await _users.GetByEmailAsync(email, ct) is not null)
                    return RegistrationResult.Failure("Email already registered");

                var user = new User
                {
                    Id           = Guid.NewGuid(),
                    Name         = name,
                    Email        = email,
                    PasswordHash = _hasher.Hash(password),
                    CreatedAt    = DateTimeOffset.UtcNow
                };

                await _users.SaveAsync(user, ct);
                await _email.SendWelcomeAsync(user, ct);
                return RegistrationResult.Ok(user.Id);
            }
        }
    }

    public static class SrpDemo
    {
        public static async Task RunAsync()
        {
            Console.WriteLine("=== SRP DEMO ===\n");

            var service = new GoodCode.UserRegistrationService(
                new GoodCode.InMemoryUserRepository(),
                new GoodCode.Pbkdf2PasswordHasher(),
                new GoodCode.ConsoleUserEmailService(),
                new GoodCode.UserRegistrationValidator());

            var result1 = await service.RegisterAsync("Alice", "alice@co.com", "Password1");
            Console.WriteLine($"Register Alice:      {(result1.Success ? $"OK id={result1.UserId}" : result1.Error)}");

            var result2 = await service.RegisterAsync("Alice", "alice@co.com", "Password1");
            Console.WriteLine($"Register Alice again: {(result2.Success ? "OK" : result2.Error)}");

            var result3 = await service.RegisterAsync("", "notanemail", "short");
            Console.WriteLine($"Invalid input:        {result3.Error}");
        }
    }
}
