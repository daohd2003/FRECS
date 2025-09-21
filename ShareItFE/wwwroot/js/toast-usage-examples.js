/**
 * Toast Manager Usage Examples
 * 
 * This file contains examples of how to use the new Toast Manager system.
 * Remove this file after reviewing the examples.
 */

// ============================================
// CLIENT-SIDE USAGE (JavaScript)
// ============================================

// Basic usage with different types
function showExampleToasts() {
    // Success toast
    toastManager.success("Operation completed successfully!");
    
    // Error toast
    toastManager.error("Something went wrong!");
    
    // Info toast  
    toastManager.info("Here's some information for you.");
    
    // Warning toast
    toastManager.warning("Please be careful!");
    
    // Custom duration (default is 5000ms)
    toastManager.success("Quick message!", 2000);
}

// Backward compatibility - old function names still work
function showLegacyCompatibility() {
    showToast("This still works!", "success");
    showSuccessToast("So does this!");
    showErrorToast("And this too!");
}

// Manual close
function showManualClose() {
    const toastId = toastManager.success("This toast can be closed manually");
    
    // Close it after 2 seconds
    setTimeout(() => {
        toastManager.close(toastId);
    }, 2000);
}

// Close all toasts
function closeAllToasts() {
    toastManager.closeAll();
}

// ============================================
// SERVER-SIDE USAGE (C#)
// ============================================

/*

// In your PageModel or Controller:
using ShareItFE.Extensions;

public class MyPageModel : PageModel 
{
    public IActionResult OnPost()
    {
        try 
        {
            // Do something...
            
            // Success toast
            TempData.AddSuccessToast("Data saved successfully!");
            
            // Error toast
            TempData.AddErrorToast("Failed to save data.");
            
            // Info toast
            TempData.AddInfoToast("Please note this information.");
            
            // Warning toast
            TempData.AddWarningToast("Please check your input.");
            
            // Custom duration (in milliseconds)
            TempData.AddSuccessToast("Quick success!", 3000);
            
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            TempData.AddErrorToast($"Error: {ex.Message}");
            return Page();
        }
    }
}

*/

// ============================================
// MIGRATION FROM OLD SYSTEM
// ============================================

/*

OLD WAY:
========
// Server-side
TempData["SuccessMessage"] = "Success!";
TempData["ErrorMessage"] = "Error!";

// Client-side  
const successElement = document.getElementById('toast-success');
if (successElement) {
    // Manual show/hide logic...
}

NEW WAY:
========
// Server-side
TempData.AddSuccessToast("Success!");
TempData.AddErrorToast("Error!");

// Client-side - automatic!
// Toast Manager automatically processes TempData and shows toasts
// No manual DOM manipulation needed!

*/

console.log("Toast Manager Usage Examples loaded. Check toast-usage-examples.js for details.");
