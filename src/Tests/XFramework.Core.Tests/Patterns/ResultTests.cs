using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using XFramework.Core.Patterns;

namespace XFramework.Core.Tests.Patterns;

/// <summary>
/// Comprehensive unit tests for the Result&lt;T&gt; pattern implementation
/// </summary>
[TestFixture]
public class ResultTests
{
    #region Result<T> Success Tests

    [Test]
    public void Success_WithValidData_ShouldCreateSuccessfulResult()
    {
        // Arrange
        var testData = "Test Data";

        // Act
        var result = Result<string>.Success(testData);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(testData);
        result.StatusCode.Should().Be(200);
        result.Message.Should().BeNull();
        result.Errors.Should().BeNull();
    }

    [Test]
    public void Success_WithValidDataAndMessage_ShouldIncludeMessage()
    {
        // Arrange
        var testData = 42;
        var message = "Operation completed successfully";

        // Act
        var result = Result<int>.Success(testData, message);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(testData);
        result.StatusCode.Should().Be(200);
        result.Message.Should().Be(message);
        result.Errors.Should().BeNull();
    }

    [Test]
    public void Success_WithCustomStatusCode_ShouldUseProvidedStatusCode()
    {
        // Arrange
        var testData = new { Id = 1, Name = "Test" };
        var statusCode = 201; // Created
        var message = "Resource created";

        // Act
        var result = Result<object>.Success(testData, statusCode, message);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeEquivalentTo(testData);
        result.StatusCode.Should().Be(201);
        result.Message.Should().Be(message);
        result.Errors.Should().BeNull();
    }

    [Test]
    public void Success_WithNullData_ShouldAllowNullData()
    {
        // Arrange
        string? nullData = null;

        // Act
        var result = Result<string?>.Success(nullData);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeNull();
        result.StatusCode.Should().Be(200);
    }

    [TestCase(200)]
    [TestCase(201)]
    [TestCase(202)]
    [TestCase(204)]
    public void Success_WithVariousSuccessStatusCodes_ShouldPreserveStatusCode(int statusCode)
    {
        // Arrange
        var testData = "Test";

        // Act
        var result = Result<string>.Success(testData, statusCode);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(statusCode);
    }

    #endregion

    #region Result<T> Failure Tests

    [Test]
    public void Failure_WithMessage_ShouldCreateFailedResult()
    {
        // Arrange
        var errorMessage = "An error occurred";

        // Act
        var result = Result<string>.Failure(errorMessage);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Data.Should().BeNull();
        result.Message.Should().Be(errorMessage);
        result.StatusCode.Should().Be(400);
        result.Errors.Should().BeNull();
    }

    [Test]
    public void Failure_WithCustomStatusCode_ShouldUseProvidedStatusCode()
    {
        // Arrange
        var errorMessage = "Server error";
        var statusCode = 500;

        // Act
        var result = Result<int>.Failure(errorMessage, statusCode);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Data.Should().Be(0); // Default value for int
        result.Message.Should().Be(errorMessage);
        result.StatusCode.Should().Be(500);
        result.Errors.Should().BeNull();
    }

    [TestCase(400)]
    [TestCase(500)]
    [TestCase(502)]
    [TestCase(503)]
    public void Failure_WithVariousErrorStatusCodes_ShouldPreserveStatusCode(int statusCode)
    {
        // Arrange
        var errorMessage = "Error";

        // Act
        var result = Result<string>.Failure(errorMessage, statusCode);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(statusCode);
        result.Message.Should().Be(errorMessage);
    }

    #endregion

    #region Result<T> NotFound Tests

    [Test]
    public void NotFound_WithoutMessage_ShouldUseDefaultMessage()
    {
        // Act
        var result = Result<string>.NotFound();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Data.Should().BeNull();
        result.StatusCode.Should().Be(404);
        result.Message.Should().Be("Resource not found");
        result.Errors.Should().BeNull();
    }

    [Test]
    public void NotFound_WithCustomMessage_ShouldUseProvidedMessage()
    {
        // Arrange
        var customMessage = "User not found";

        // Act
        var result = Result<object>.NotFound(customMessage);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Data.Should().BeNull();
        result.StatusCode.Should().Be(404);
        result.Message.Should().Be(customMessage);
        result.Errors.Should().BeNull();
    }

    [Test]
    public void NotFound_WithNullMessage_ShouldAcceptNull()
    {
        // Act
        var result = Result<string>.NotFound(null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        result.Message.Should().BeNull();
    }

    #endregion

    #region Result<T> Unauthorized Tests

    [Test]
    public void Unauthorized_WithoutMessage_ShouldUseDefaultMessage()
    {
        // Act
        var result = Result<string>.Unauthorized();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Data.Should().BeNull();
        result.StatusCode.Should().Be(401);
        result.Message.Should().Be("Unauthorized");
        result.Errors.Should().BeNull();
    }

    [Test]
    public void Unauthorized_WithCustomMessage_ShouldUseProvidedMessage()
    {
        // Arrange
        var customMessage = "Invalid credentials";

        // Act
        var result = Result<object>.Unauthorized(customMessage);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Data.Should().BeNull();
        result.StatusCode.Should().Be(401);
        result.Message.Should().Be(customMessage);
        result.Errors.Should().BeNull();
    }

    [Test]
    public void Unauthorized_WithNullMessage_ShouldAcceptNull()
    {
        // Act
        var result = Result<string>.Unauthorized(null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);
        result.Message.Should().BeNull();
    }

    #endregion

    #region Result<T> Forbidden Tests

    [Test]
    public void Forbidden_WithoutMessage_ShouldUseDefaultMessage()
    {
        // Act
        var result = Result<string>.Forbidden();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Data.Should().BeNull();
        result.StatusCode.Should().Be(403);
        result.Message.Should().Be("Forbidden");
        result.Errors.Should().BeNull();
    }

    [Test]
    public void Forbidden_WithCustomMessage_ShouldUseProvidedMessage()
    {
        // Arrange
        var customMessage = "Insufficient permissions";

        // Act
        var result = Result<object>.Forbidden(customMessage);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Data.Should().BeNull();
        result.StatusCode.Should().Be(403);
        result.Message.Should().Be(customMessage);
        result.Errors.Should().BeNull();
    }

    [Test]
    public void Forbidden_WithNullMessage_ShouldAcceptNull()
    {
        // Act
        var result = Result<string>.Forbidden(null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.Message.Should().BeNull();
    }

    #endregion

    #region Result<T> Conflict Tests

    [Test]
    public void Conflict_WithoutMessage_ShouldUseDefaultMessage()
    {
        // Act
        var result = Result<string>.Conflict();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Data.Should().BeNull();
        result.StatusCode.Should().Be(409);
        result.Message.Should().Be("Conflict");
        result.Errors.Should().BeNull();
    }

    [Test]
    public void Conflict_WithCustomMessage_ShouldUseProvidedMessage()
    {
        // Arrange
        var customMessage = "Resource already exists";

        // Act
        var result = Result<object>.Conflict(customMessage);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Data.Should().BeNull();
        result.StatusCode.Should().Be(409);
        result.Message.Should().Be(customMessage);
        result.Errors.Should().BeNull();
    }

    [Test]
    public void Conflict_WithNullMessage_ShouldAcceptNull()
    {
        // Act
        var result = Result<string>.Conflict(null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.Message.Should().BeNull();
    }

    #endregion

    #region Result<T> ValidationError Tests

    [Test]
    public void ValidationError_WithErrors_ShouldCreateValidationErrorResult()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>
        {
            { "Email", new[] { "Email is required", "Email format is invalid" } },
            { "Password", new[] { "Password must be at least 8 characters" } }
        };

        // Act
        var result = Result<object>.ValidationError(errors);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Data.Should().BeNull();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Be("Validation failed");
        result.Errors.Should().NotBeNull();
        result.Errors.Should().BeEquivalentTo(errors);
    }

    [Test]
    public void ValidationError_WithCustomMessage_ShouldUseProvidedMessage()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>
        {
            { "Field", new[] { "Error" } }
        };
        var customMessage = "Custom validation message";

        // Act
        var result = Result<string>.ValidationError(errors, customMessage);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Be(customMessage);
        result.Errors.Should().BeEquivalentTo(errors);
    }

    [Test]
    public void ValidationError_WithEmptyErrorDictionary_ShouldAcceptEmptyDictionary()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>();

        // Act
        var result = Result<object>.ValidationError(errors);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Errors.Should().NotBeNull();
        result.Errors.Should().BeEmpty();
    }

    [Test]
    public void ValidationError_WithNullMessage_ShouldAcceptNull()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>
        {
            { "Field", new[] { "Error" } }
        };

        // Act
        var result = Result<string>.ValidationError(errors, null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().BeNull();
        result.Errors.Should().BeEquivalentTo(errors);
    }

    [Test]
    public void ValidationError_WithLargeErrorCollection_ShouldHandleLargeCollections()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>();
        for (int i = 0; i < 100; i++)
        {
            errors[$"Field{i}"] = Enumerable.Range(0, 10)
                .Select(j => $"Error {j} for Field{i}")
                .ToArray();
        }

        // Act
        var result = Result<object>.ValidationError(errors);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().NotBeNull();
        result.Errors.Should().HaveCount(100);
        result.Errors!["Field0"].Should().HaveCount(10);
    }

    #endregion

    #region Non-Generic Result Success Tests

    [Test]
    public void NonGenericResult_Success_WithoutMessage_ShouldCreateSuccessfulResult()
    {
        // Act
        var result = Result.Success();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Message.Should().BeNull();
        result.Errors.Should().BeNull();
    }

    [Test]
    public void NonGenericResult_Success_WithMessage_ShouldIncludeMessage()
    {
        // Arrange
        var message = "Operation completed";

        // Act
        var result = Result.Success(message);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Message.Should().Be(message);
        result.Errors.Should().BeNull();
    }

    [Test]
    public void NonGenericResult_Success_WithNullMessage_ShouldAcceptNull()
    {
        // Act
        var result = Result.Success(null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Message.Should().BeNull();
    }

    #endregion

    #region Non-Generic Result Failure Tests

    [Test]
    public void NonGenericResult_Failure_WithMessage_ShouldCreateFailedResult()
    {
        // Arrange
        var errorMessage = "An error occurred";

        // Act
        var result = Result.Failure(errorMessage);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Be(errorMessage);
        result.Errors.Should().BeNull();
    }

    [Test]
    public void NonGenericResult_Failure_WithCustomStatusCode_ShouldUseProvidedStatusCode()
    {
        // Arrange
        var errorMessage = "Server error";
        var statusCode = 500;

        // Act
        var result = Result.Failure(errorMessage, statusCode);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(500);
        result.Message.Should().Be(errorMessage);
        result.Errors.Should().BeNull();
    }

    #endregion

    #region Non-Generic Result ValidationError Tests

    [Test]
    public void NonGenericResult_ValidationError_WithErrors_ShouldCreateValidationErrorResult()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>
        {
            { "Name", new[] { "Name is required" } },
            { "Age", new[] { "Age must be positive" } }
        };

        // Act
        var result = Result.ValidationError(errors);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Be("Validation failed");
        result.Errors.Should().BeEquivalentTo(errors);
    }

    [Test]
    public void NonGenericResult_ValidationError_WithCustomMessage_ShouldUseProvidedMessage()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>
        {
            { "Field", new[] { "Error" } }
        };
        var customMessage = "Custom validation error";

        // Act
        var result = Result.ValidationError(errors, customMessage);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Be(customMessage);
        result.Errors.Should().BeEquivalentTo(errors);
    }

    [Test]
    public void NonGenericResult_ValidationError_WithEmptyDictionary_ShouldAcceptEmptyDictionary()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>();

        // Act
        var result = Result.ValidationError(errors);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().NotBeNull();
        result.Errors.Should().BeEmpty();
    }

    #endregion

    #region Non-Generic Result NotFound Tests

    [Test]
    public void NonGenericResult_NotFound_WithoutMessage_ShouldUseDefaultMessage()
    {
        // Act
        var result = Result.NotFound();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        result.Message.Should().Be("Resource not found");
        result.Errors.Should().BeNull();
    }

    [Test]
    public void NonGenericResult_NotFound_WithCustomMessage_ShouldUseProvidedMessage()
    {
        // Arrange
        var customMessage = "Item not found";

        // Act
        var result = Result.NotFound(customMessage);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
        result.Message.Should().Be(customMessage);
        result.Errors.Should().BeNull();
    }

    #endregion

    #region Edge Cases and Null Handling Tests

    [Test]
    public void Failure_WithNullMessage_ShouldStillCreateResult()
    {
        // Act
        var result = Result<string>.Failure(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().BeNull();
    }

    [Test]
    public void ValidationError_WithMultipleErrorsPerField_ShouldPreserveAllErrors()
    {
        // Arrange
        var errors = new Dictionary<string, string[]>
        {
            { "Email", new[] { "Required", "Invalid format", "Already exists", "Too long" } }
        };

        // Act
        var result = Result<object>.ValidationError(errors);

        // Assert
        result.Errors.Should().NotBeNull();
        result.Errors!["Email"].Should().HaveCount(4);
        result.Errors["Email"].Should().Contain(new[] { "Required", "Invalid format", "Already exists", "Too long" });
    }

    [Test]
    public void Result_WithComplexDataType_ShouldPreserveDataStructure()
    {
        // Arrange
        var complexData = new
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Items = new[] { 1, 2, 3 },
            Metadata = new Dictionary<string, object>
            {
                { "CreatedAt", DateTime.UtcNow },
                { "IsActive", true }
            }
        };

        // Act
        var result = Result<object>.Success(complexData, "Created successfully");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeEquivalentTo(complexData);
        result.Message.Should().Be("Created successfully");
    }

    [Test]
    public void Result_WithEmptyString_ShouldPreserveEmptyString()
    {
        // Act
        var result = Result<string>.Success(string.Empty);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(string.Empty);
        result.Data.Should().NotBeNull();
    }

    [Test]
    public void Result_AsRecord_ShouldSupportEqualityComparison()
    {
        // Arrange
        var result1 = Result<int>.Success(42, "Success");
        var result2 = Result<int>.Success(42, "Success");
        var result3 = Result<int>.Success(43, "Success");

        // Assert
        result1.Should().Be(result2); // Records with same values are equal
        result1.Should().NotBe(result3); // Different values
    }

    [Test]
    public void Result_WithInit_ShouldBeImmutable()
    {
        // Arrange
        var result = Result<string>.Success("Original");

        // Act & Assert
        // This should not compile if uncommented (init-only properties)
        // result.Data = "Modified"; // Compiler error expected
        // result.IsSuccess = false; // Compiler error expected

        result.Data.Should().Be("Original");
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Integration Tests - Different Data Types

    [Test]
    public void Result_WithValueType_ShouldWorkCorrectly()
    {
        // Act
        var intResult = Result<int>.Success(123);
        var boolResult = Result<bool>.Success(true);
        var dateResult = Result<DateTime>.Success(DateTime.UtcNow);

        // Assert
        intResult.Data.Should().Be(123);
        boolResult.Data.Should().BeTrue();
        dateResult.Data.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Test]
    public void Result_WithReferenceType_ShouldWorkCorrectly()
    {
        // Arrange
        var list = new List<string> { "one", "two", "three" };

        // Act
        var result = Result<List<string>>.Success(list);

        // Assert
        result.Data.Should().BeSameAs(list);
        result.Data.Should().HaveCount(3);
    }

    [Test]
    public void Result_WithNullableValueType_ShouldWorkCorrectly()
    {
        // Arrange
        int? nullableValue = 42;
        int? nullValue = null;

        // Act
        var resultWithValue = Result<int?>.Success(nullableValue);
        var resultWithNull = Result<int?>.Success(nullValue);

        // Assert
        resultWithValue.Data.Should().Be(42);
        resultWithNull.Data.Should().BeNull();
    }

    #endregion

    #region Status Code Verification Tests

    [TestCase(200, "OK")]
    [TestCase(201, "Created")]
    [TestCase(204, "No Content")]
    [TestCase(400, "Bad Request")]
    [TestCase(401, "Unauthorized")]
    [TestCase(403, "Forbidden")]
    [TestCase(404, "Not Found")]
    [TestCase(409, "Conflict")]
    [TestCase(500, "Internal Server Error")]
    public void Result_WithVariousHttpStatusCodes_ShouldPreserveStatusCode(int statusCode, string description)
    {
        // Act
        var successResult = Result<string>.Success("test", statusCode, description);
        var failureResult = Result<string>.Failure(description, statusCode);

        // Assert
        if (statusCode < 400)
        {
            successResult.StatusCode.Should().Be(statusCode);
            successResult.Message.Should().Be(description);
        }
        else
        {
            failureResult.StatusCode.Should().Be(statusCode);
            failureResult.Message.Should().Be(description);
        }
    }

    #endregion
}