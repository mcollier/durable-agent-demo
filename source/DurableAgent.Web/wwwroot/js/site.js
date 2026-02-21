// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Phone number formatting utility
function formatPhoneNumber(value) {
    const digits = value.replace(/\D/g, '').substring(0, 10);
    const len = digits.length;
    if (len === 0) return '';
    if (len <= 3) return `(${digits}`;
    if (len <= 6) return `(${digits.substring(0, 3)}) ${digits.substring(3)}`;
    return `(${digits.substring(0, 3)}) ${digits.substring(3, 6)}-${digits.substring(6)}`;
}

// Apply phone formatting to an input field
function applyPhoneFormatting(inputElement) {
    if (!inputElement) return;

    const formatInput = function() {
        const cursorPosition = inputElement.selectionStart;
        const oldValue = inputElement.value;
        const oldLength = oldValue.length;
        
        inputElement.value = formatPhoneNumber(oldValue);
        
        const newLength = inputElement.value.length;
        const diff = newLength - oldLength;
        
        // Adjust cursor position if formatting added characters
        if (diff > 0 && cursorPosition < newLength) {
            inputElement.setSelectionRange(cursorPosition + diff, cursorPosition + diff);
        } else {
            inputElement.setSelectionRange(cursorPosition, cursorPosition);
        }
    };

    inputElement.addEventListener('input', formatInput);
    inputElement.addEventListener('paste', function() {
        setTimeout(formatInput, 0);
    });
}
