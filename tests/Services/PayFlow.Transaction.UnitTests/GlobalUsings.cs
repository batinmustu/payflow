global using Xunit;
global using FluentAssertions;
global using PayFlow.SharedKernel;
global using PayFlow.Transaction.Domain;
global using PayFlow.Transaction.Domain.Transactions;
// Alias to disambiguate the `Transaction` type from the `PayFlow.Transaction`
// namespace (the test project lives under that root, so a bare `Transaction`
// resolves to the namespace first).
global using TransactionAggregate = PayFlow.Transaction.Domain.Transactions.Transaction;
