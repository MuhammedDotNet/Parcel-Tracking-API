using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using ParcelTracking.Application.DTOs;
using ParcelTracking.Domain.Enums;
using ParcelTracking.Domain.Rules;

namespace ParcelTracking.Application.UnitTests.DTOs;

public class ErrorResponseConsistencyTests
{
    // Feature: parcel-status-lifecycle, Property 16: Error response consistency
    // Validates: Requirements 11.1, 11.5
    [Property(MaxTest = 100)]
    public void AllErrorResponses_HaveConsistentStructure(
        int parcelId,
        ParcelStatus currentStatus,
        ParcelStatus requestedStatus)
    {
        // Ensure we have valid IDs
        var validParcelId = Math.Abs(parcelId) + 1;

        // Test 1: NotFound error response
        var notFoundResult = StatusTransitionResult.NotFound(validParcelId);
        
        // Verify NotFound has required fields
        notFoundResult.IsSuccess.Should().BeFalse("error responses should have IsSuccess=false");
        notFoundResult.ErrorType.Should().NotBeNullOrEmpty("all error responses must have 'error' field");
        notFoundResult.ErrorMessage.Should().NotBeNullOrEmpty("all error responses must have 'message' field");
        notFoundResult.ErrorType.Should().Be("not_found", "error type should be consistent");
        
        // Verify NotFound includes parcel ID in context (both in message and as separate field)
        notFoundResult.ErrorMessage.Should().Contain(validParcelId.ToString(),
            "not found errors must include parcel ID in message");
        notFoundResult.ParcelId.Should().Be(validParcelId,
            "not found errors must include parcel ID as separate field");

        // Test 2: TerminalState error response
        // Only test with actual terminal states
        var terminalStatuses = new[] { ParcelStatus.Delivered, ParcelStatus.Returned };
        foreach (var terminalStatus in terminalStatuses)
        {
            var terminalResult = StatusTransitionResult.TerminalState(terminalStatus);
            
            // Verify TerminalState has required fields
            terminalResult.IsSuccess.Should().BeFalse("error responses should have IsSuccess=false");
            terminalResult.ErrorType.Should().NotBeNullOrEmpty("all error responses must have 'error' field");
            terminalResult.ErrorMessage.Should().NotBeNullOrEmpty("all error responses must have 'message' field");
            terminalResult.ErrorType.Should().Be("terminal_state", "error type should be consistent");
            
            // Verify TerminalState includes context (currentStatus)
            terminalResult.CurrentStatus.Should().NotBeNull("terminal state errors must include current status");
            terminalResult.CurrentStatus.Should().Be(terminalStatus);
            terminalResult.ErrorMessage.Should().Contain(terminalStatus.ToString(),
                "terminal state error message should mention the status");
        }

        // Test 3: InvalidTransition error response
        // Get allowed transitions for the current status
        var allowedStatuses = ParcelStatusRules.GetAllowedTransitions(currentStatus);
        
        var invalidTransitionResult = StatusTransitionResult.InvalidTransition(
            currentStatus,
            requestedStatus,
            allowedStatuses);
        
        // Verify InvalidTransition has required fields
        invalidTransitionResult.IsSuccess.Should().BeFalse("error responses should have IsSuccess=false");
        invalidTransitionResult.ErrorType.Should().NotBeNullOrEmpty("all error responses must have 'error' field");
        invalidTransitionResult.ErrorMessage.Should().NotBeNullOrEmpty("all error responses must have 'message' field");
        invalidTransitionResult.ErrorType.Should().Be("invalid_transition", "error type should be consistent");
        
        // Verify InvalidTransition includes context (currentStatus, requestedStatus, allowedStatuses)
        invalidTransitionResult.CurrentStatus.Should().NotBeNull(
            "invalid transition errors must include current status");
        invalidTransitionResult.CurrentStatus.Should().Be(currentStatus);
        invalidTransitionResult.RequestedStatus.Should().NotBeNull(
            "invalid transition errors must include requested status");
        invalidTransitionResult.RequestedStatus.Should().Be(requestedStatus);
        invalidTransitionResult.AllowedStatuses.Should().NotBeNull(
            "invalid transition errors must include allowed statuses");
        invalidTransitionResult.AllowedStatuses.Should().BeEquivalentTo(allowedStatuses);
        
        // Verify message contains both statuses
        invalidTransitionResult.ErrorMessage.Should().Contain(currentStatus.ToString(),
            "invalid transition error message should mention current status");
        invalidTransitionResult.ErrorMessage.Should().Contain(requestedStatus.ToString(),
            "invalid transition error message should mention requested status");
    }

    [Property(MaxTest = 100)]
    public void ErrorResponses_UseConsistentFieldNames(int parcelId, ParcelStatus status1, ParcelStatus status2)
    {
        // Ensure valid ID
        var validParcelId = Math.Abs(parcelId) + 1;
        
        // Get all error types
        var notFoundResult = StatusTransitionResult.NotFound(validParcelId);
        var terminalResult = StatusTransitionResult.TerminalState(ParcelStatus.Delivered);
        var allowedStatuses = ParcelStatusRules.GetAllowedTransitions(status1);
        var invalidTransitionResult = StatusTransitionResult.InvalidTransition(status1, status2, allowedStatuses);

        // All error types should use the same field name for error type
        notFoundResult.ErrorType.Should().NotBeNull("ErrorType field should be present");
        terminalResult.ErrorType.Should().NotBeNull("ErrorType field should be present");
        invalidTransitionResult.ErrorType.Should().NotBeNull("ErrorType field should be present");

        // All error types should use the same field name for error message
        notFoundResult.ErrorMessage.Should().NotBeNull("ErrorMessage field should be present");
        terminalResult.ErrorMessage.Should().NotBeNull("ErrorMessage field should be present");
        invalidTransitionResult.ErrorMessage.Should().NotBeNull("ErrorMessage field should be present");

        // Field names should be consistent (using the same property names)
        // This is enforced by the type system, but we verify the values are set
        var allErrorTypes = new[] 
        { 
            notFoundResult.ErrorType, 
            terminalResult.ErrorType, 
            invalidTransitionResult.ErrorType 
        };
        
        allErrorTypes.Should().AllSatisfy(errorType => 
            errorType.Should().NotBeNullOrEmpty("all error types should have a value"));

        var allErrorMessages = new[] 
        { 
            notFoundResult.ErrorMessage, 
            terminalResult.ErrorMessage, 
            invalidTransitionResult.ErrorMessage 
        };
        
        allErrorMessages.Should().AllSatisfy(message => 
            message.Should().NotBeNullOrEmpty("all error messages should have a value"));
    }

    [Property(MaxTest = 100)]
    public void NotFoundErrors_AlwaysIncludeParcelId(PositiveInt parcelId)
    {
        // Act
        var result = StatusTransitionResult.NotFound(parcelId.Get);

        // Assert
        result.ErrorMessage.Should().Contain(parcelId.Get.ToString(),
            "not found error messages must include the parcel ID for debugging");
        result.ErrorType.Should().Be("not_found");
        result.IsSuccess.Should().BeFalse();
    }
}
