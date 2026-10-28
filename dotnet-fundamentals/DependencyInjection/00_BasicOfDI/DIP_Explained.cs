// ============================================================
// DIP_Explained.cs
// Dependency Inversion Principle — from violation to correct design
//
// DIP has two rules (Robert C. Martin, "Agile Principles, Patterns, and Practices"):
//   1. High-level modules must NOT depend on low-level modules.
//      Both must depend on abstractions.
//   2. Abstractions must NOT depend on details.
//      Details must depend on abstractions.
// ============================================================

// ── Problem 1: High-level module depends directly on a low-level concrete class ──────────────────

namespace DI.Concepts.BasicOfDI.DIP.Violation
{
    // ── Low-level module: infrastructure concern ─────────────────────────────────────────────────
    // Knows HOW to persist data — that's its job.
    public class SqlOrderRepository
    {
        public void Save(string order)
        {
            // Hard-wired SQL logic here. Perfectly fine in isolation.
            Console.WriteLine($"[SQL] Saving order: {order}");
        }

        public string? GetById(int id)
        {
            return $"Order #{id} from SQL";
        }
    }

    public class SmtpEmailSender
    {
        public void Send(string to, string body)
        {
            // Hard-wired SMTP logic here.
            Console.WriteLine($"[SMTP] Sending email to {to}: {body}");
        }
    }

    // ── High-level module: business logic ────────────────────────────────────────────────────────
    // DIP VIOLATION: OrderService knows the concrete type SqlOrderRepository.
    // Arrow: OrderService ──► SqlOrderRepository  (wrong direction)
    public class OrderService_Violating
    {
        // Hard dependency on a concrete class — can't swap without editing this file.
        private readonly SqlOrderRepository _repository = new SqlOrderRepository();
        private readonly SmtpEmailSender _emailSender = new SmtpEmailSender();

        public void PlaceOrder(string orderDetails)
        {
            _repository.Save(orderDetails);   // coupled to SQL
            _emailSender.Send("customer@example.com", $"Order placed: {orderDetails}");  // coupled to SMTP

            // Problems with this approach:
            // 1. Can't unit-test OrderService without hitting a real database AND real SMTP.
            // 2. Switching from SQL to Cosmos DB requires editing OrderService — a business-logic class.
            // 3. Switching from SMTP to SendGrid requires editing OrderService — same problem.
            // 4. OrderService violates Single Responsibility: it also controls how it gets its tools.
        }
    }
}

// ── Problem 2: Abstraction owned by the wrong layer ──────────────────────────────────────────────

namespace DI.Concepts.BasicOfDI.DIP.WrongOwnership
{
    // BAD: the interface lives in the infrastructure layer.
    // The business layer is forced to reference infrastructure — the dependency arrow still points down.
    namespace Infrastructure
    {
        public interface IOrderRepository  // ← owned by infrastructure, NOT business
        {
            void Save(string order);
        }

        public class SqlOrderRepository : IOrderRepository
        {
            public void Save(string order) => Console.WriteLine($"[SQL] {order}");
        }
    }

    namespace Business
    {
        // Business still references Infrastructure namespace to get the interface.
        // The coupling is hidden but still there.
        public class OrderService
        {
            private readonly Infrastructure.IOrderRepository _repo;

            public OrderService(Infrastructure.IOrderRepository repo)  // still depends on infra namespace
            {
                _repo = repo;
            }
        }
    }
}

// ── Solution: DIP Applied Correctly ──────────────────────────────────────────────────────────────

namespace DI.Concepts.BasicOfDI.DIP.Correct
{
    // ── Step 1: Define abstractions in the business/domain layer ─────────────────────────────────
    // The interface is OWNED by the business layer. Infrastructure depends on business, not the other way.
    // Arrow: SqlOrderRepository ──► IOrderRepository ◄── OrderService
    //        (detail points to abstraction)  (high-level points to abstraction)

    public interface IOrderRepository
    {
        void Save(string order);
        string? GetById(int id);
    }

    public interface IEmailSender
    {
        void Send(string to, string body);
    }

    // ── Step 2: Business logic depends only on the abstraction ───────────────────────────────────
    public class OrderService
    {
        private readonly IOrderRepository _repository;
        private readonly IEmailSender _emailSender;

        // Constructor injection: dependencies are provided externally.
        // OrderService has no idea whether it's talking to SQL, Cosmos, or a fake.
        public OrderService(IOrderRepository repository, IEmailSender emailSender)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
        }

        public void PlaceOrder(string orderDetails)
        {
            _repository.Save(orderDetails);
            _emailSender.Send("customer@example.com", $"Order placed: {orderDetails}");
        }
    }

    // ── Step 3: Low-level details depend on the abstraction (not the other way around) ───────────
    // Infrastructure layer implements the interface defined by the business layer.

    public class SqlOrderRepository : IOrderRepository
    {
        public void Save(string order) => Console.WriteLine($"[SQL] Saving: {order}");
        public string? GetById(int id) => $"Order #{id} from SQL";
    }

    public class CosmosOrderRepository : IOrderRepository
    {
        public void Save(string order) => Console.WriteLine($"[Cosmos] Saving: {order}");
        public string? GetById(int id) => $"Order #{id} from Cosmos";
    }

    public class SmtpEmailSender : IEmailSender
    {
        public void Send(string to, string body) => Console.WriteLine($"[SMTP] To {to}: {body}");
    }

    public class SendGridEmailSender : IEmailSender
    {
        public void Send(string to, string body) => Console.WriteLine($"[SendGrid] To {to}: {body}");
    }

    // ── Step 4: Test fakes are now trivially possible ────────────────────────────────────────────
    // Because OrderService depends on interfaces, tests can inject controlled fakes.

    public class FakeOrderRepository : IOrderRepository
    {
        public List<string> Saved { get; } = new();

        public void Save(string order) => Saved.Add(order);
        public string? GetById(int id) => Saved.ElementAtOrDefault(id);
    }

    public class FakeEmailSender : IEmailSender
    {
        public List<(string To, string Body)> SentEmails { get; } = new();

        public void Send(string to, string body) => SentEmails.Add((to, body));
    }

    // ── Composition Root: the one place that wires details to abstractions ───────────────────────
    // "New" keywords live HERE — not inside business-logic classes.
    public static class CompositionRoot
    {
        public static OrderService BuildProductionService()
        {
            // Swap SqlOrderRepository for CosmosOrderRepository here — OrderService never changes.
            IOrderRepository repo = new SqlOrderRepository();
            IEmailSender emailSender = new SmtpEmailSender();
            return new OrderService(repo, emailSender);
        }

        public static OrderService BuildTestService(out FakeOrderRepository fakeRepo, out FakeEmailSender fakeEmail)
        {
            fakeRepo = new FakeOrderRepository();
            fakeEmail = new FakeEmailSender();
            return new OrderService(fakeRepo, fakeEmail);
        }
    }

    // ── Demo ─────────────────────────────────────────────────────────────────────────────────────
    public class DIPDemo
    {
        public static void Run()
        {
            Console.WriteLine("=== DIP: Correct Application ===\n");

            // Production: real SQL + SMTP
            var productionService = CompositionRoot.BuildProductionService();
            productionService.PlaceOrder("Widget x2");

            Console.WriteLine();

            // Test: fakes — no real DB, no real SMTP
            var testService = CompositionRoot.BuildTestService(out var fakeRepo, out var fakeEmail);
            testService.PlaceOrder("Gadget x1");

            Console.WriteLine($"\nFake repo captured {fakeRepo.Saved.Count} order(s).");
            Console.WriteLine($"Fake email captured {fakeEmail.SentEmails.Count} message(s).");
        }
    }
}
