// Fix for Compensation Details page - VND currency and validation

// Override updateDecisionSummary function
window.updateDecisionSummaryFixed = function() {
    const resolutionType = $('input[name="resolution-type"]:checked').val();
    const penaltyAmount = parseFloat($('#penalty-amount').val()) || 0;
    const adminNotes = $('#admin-notes').val().trim();

    if (!resolutionType) {
        $('#decision-summary').addClass('hidden');
        return;
    }

    let finalAmount = 0;
    if (resolutionType === 'UPHOLD_CLAIM') {
        finalAmount = window.disputeData.requestedCompensation;
    } else if (resolutionType === 'COMPROMISE') {
        finalAmount = penaltyAmount;
    }

    const summaryHtml = `
        <li>• Resolution: <span class="font-semibold">${resolutionType.replace(/_/g, ' ')}</span></li>
        <li>• Penalty: <span class="font-semibold">₫${finalAmount.toLocaleString('vi-VN')}</span></li>
        <li>• Both parties will be notified via email</li>
        ${adminNotes ? '<li>• Verdict notes provided</li>' : ''}
    `;

    $('#summary-content').html(summaryHtml);
    $('#decision-summary').removeClass('hidden');
};

// Override submitDecision validation
window.submitDecisionFixed = async function() {
    const resolutionType = $('input[name="resolution-type"]:checked').val();
    const penaltyAmountInput = $('#penalty-amount').val();
    const adminNotes = $('#admin-notes').val().trim();

    // Validation
    if (!resolutionType) {
        alert('Please select a resolution type');
        return;
    }

    if (!adminNotes) {
        alert('Please provide verdict notes explaining your decision');
        return;
    }

    if (adminNotes.length < 10) {
        alert('Verdict notes must be at least 10 characters long');
        return;
    }

    let customerFineAmount = 0;
    let providerCompensationAmount = 0;

    if (resolutionType === 'UPHOLD_CLAIM') {
        customerFineAmount = window.disputeData.requestedCompensation;
        providerCompensationAmount = window.disputeData.requestedCompensation;
    } else if (resolutionType === 'COMPROMISE') {
        if (!penaltyAmountInput) {
            alert('Please enter a penalty amount for the compromise');
            return;
        }
        const penaltyAmount = parseFloat(penaltyAmountInput);
        const maxAmount = window.disputeData.orderItem?.totalDeposit || 0;
        
        if (penaltyAmount < 0 || penaltyAmount > maxAmount) {
            alert(`Penalty amount must be between ₫0 and ₫${maxAmount.toLocaleString('vi-VN')}`);
            return;
        }
        customerFineAmount = penaltyAmount;
        providerCompensationAmount = penaltyAmount;
    }

    // Call original submit logic
    if (window.submitDecisionOriginal) {
        return window.submitDecisionOriginal(resolutionType, customerFineAmount, providerCompensationAmount, adminNotes);
    }
};

// Apply fixes when page loads
$(document).ready(function() {
    // Replace functions
    if (typeof updateDecisionSummary !== 'undefined') {
        window.updateDecisionSummary = window.updateDecisionSummaryFixed;
    }
    
    // Store original submit function
    if (typeof submitDecision !== 'undefined') {
        window.submitDecisionOriginal = submitDecision;
        window.submitDecision = window.submitDecisionFixed;
    }
});
