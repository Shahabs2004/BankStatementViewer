using BankStatementViewer.Models;
using BankStatementViewer.Services;
using Xunit;

namespace BankStatementViewer.Tests;

public sealed class TransactionDescriptionParserTests
{
    [Theory]
    [InlineData("Deposit confirmed")]
    [InlineData("purpose: gift")]
    public void Populate_DoesNotDetectPosInsideOtherLatinWords(string description)
    {
        var transaction = new StatementTransaction();
        TransactionDescriptionParser.Populate(transaction, description);
        Assert.NotEqual("POS", transaction.Channel);
    }

    [Fact]
    public void Populate_DetectsMobAttachedToPersianText()
    {
        var transaction = new StatementTransaction();
        TransactionDescriptionParser.Populate(transaction, "انتقالMOB");
        Assert.Equal("MOB", transaction.Channel);
    }

    [Fact]
    public void Populate_DetectsPersianPosOperation()
    {
        var transaction = new StatementTransaction();
        TransactionDescriptionParser.Populate(transaction, "خریدباPOS");
        Assert.Equal("POS", transaction.Channel);
    }

    [Fact]
    public void Populate_DoesNotDetectFeeInsideCoffee()
    {
        var transaction = new StatementTransaction();
        TransactionDescriptionParser.Populate(transaction, "coffee");
        Assert.False(transaction.IsFee);
    }

    [Fact]
    public void Populate_DetectsStandaloneFeeToken()
    {
        var transaction = new StatementTransaction();
        TransactionDescriptionParser.Populate(transaction, "fee");
        Assert.True(transaction.IsFee);
    }
    [Fact]
    public void Populate_DetectsFeeWithSpace()
    {
        var transaction = new StatementTransaction();
        TransactionDescriptionParser.Populate(transaction, "service fee");
        Assert.True(transaction.IsFee);
    }

    [Fact]
    public void Populate_DetectsMobWithSpaceAfterPersianText()
    {
        var transaction = new StatementTransaction();
        TransactionDescriptionParser.Populate(transaction, "انتقال MOB");
        Assert.Equal("MOB", transaction.Channel);
    }

    [Fact]
    public void Populate_DetectsPosAtStartOfDescription()
    {
        var transaction = new StatementTransaction();
        TransactionDescriptionParser.Populate(transaction, "POS purchase");
        Assert.Equal("POS", transaction.Channel);
    }

    [Fact]
    public void Populate_DoesNotDetectFeeInsideCoffeeWithPrefix()
    {
        var transaction = new StatementTransaction();
        TransactionDescriptionParser.Populate(transaction, "bill coffee");
        Assert.False(transaction.IsFee);
    }

}
