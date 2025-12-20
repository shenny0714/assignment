document.addEventListener("DOMContentLoaded", function () {

    // --- Dark Mode  ---
    const themeToggleBtn = document.getElementById('theme-toggle');
    const themeIcon = themeToggleBtn.querySelector('.material-symbols-outlined');
    const body = document.body;

    const currentTheme = localStorage.getItem('theme');
    if (currentTheme === 'dark') {
        body.classList.add('dark-mode');
        themeIcon.textContent = 'light_mode';
    }

    themeToggleBtn.addEventListener('click', function () {
        body.classList.toggle('dark-mode');

        if (body.classList.contains('dark-mode')) {
            localStorage.setItem('theme', 'dark');
            themeIcon.textContent = 'light_mode';
        } else {
            localStorage.setItem('theme', 'light');
            themeIcon.textContent = 'dark_mode';
        }
    });

});

// Initiate GET request (AJAX-supported)
$(document).on('click', '[data-get]', e => {
    e.preventDefault();
    const url = e.target.dataset.get;
    location = url || location;
});

// Initiate POST request (AJAX-supported)
$(document).on('click', '[data-post]', e => {
    e.preventDefault();
    const url = e.target.dataset.post;
    const f = $('<form>').appendTo(document.body)[0];
    f.method = 'post';
    f.action = url || location;
    f.submit();
});

// Trim input
$('[data-trim]').on('change', e => {
    e.target.value = e.target.value.trim();
});

// Auto uppercase
$('[data-upper]').on('input', e => {
    const a = e.target.selectionStart;
    const b = e.target.selectionEnd;
    e.target.value = e.target.value.toUpperCase();
    e.target.setSelectionRange(a, b);
});

// RESET form
$('[type=reset]').on('click', e => {
    e.preventDefault();
    location = location;
});

// Check all checkboxes
$('[data-check]').on('click', e => {
    e.preventDefault();
    const name = e.target.dataset.check;
    $(`[name=${name}]`).prop('checked', true);
});

// Uncheck all checkboxes
$('[data-uncheck]').on('click', e => {
    e.preventDefault();
    const name = e.target.dataset.uncheck;
    $(`[name=${name}]`).prop('checked', false);
});

// Row checkable (AJAX-supported)
$(document).on('click', '[data-checkable]', e => {
    if ($(e.target).is(':input,a')) return;
    
    $(e.currentTarget)
        .find(':checkbox')
        .prop('checked', (i, v) => !v);
});

// PROFILE DROPDOWN MENU LOGIC
document.addEventListener('DOMContentLoaded', function () {
    const trigger = document.getElementById('profileTrigger');
    const dropdown = document.getElementById('profileDropdown');

    if (trigger && dropdown) {
        // Toggle menu on click
        trigger.addEventListener('click', function (e) {
            e.stopPropagation();
            dropdown.classList.toggle('show');
        });

        // Close menu if clicking anywhere else on the screen
        document.addEventListener('click', function (e) {
            if (!dropdown.contains(e.target) && !trigger.contains(e.target)) {
                dropdown.classList.remove('show');
            }
        });
    }
});



// 1. IMAGE PREVIEW LOGIC
function previewImage(input) {
    const previewContainer = document.getElementById('previewContainer');
    const previewImg = document.getElementById('previewImg');

    if (input.files && input.files[0]) {
        var reader = new FileReader();
        reader.onload = function (e) {
            previewImg.src = e.target.result;
            previewContainer.classList.remove('hidden');
        }
        reader.readAsDataURL(input.files[0]);
    } else {
        previewContainer.classList.add('hidden');
    }
}

// 2. EDIT MODE TOGGLE
let originalName = "";
let originalPhone = "";

function toggleEditMode(enable) {
    const inputName = document.getElementById('inputName');
    const inputPhone = document.getElementById('inputPhone');
    const photoGroup = document.getElementById('photoUploadGroup');
    const actionsDiv = document.getElementById('personalInfoActions');
    const editBtn = document.getElementById('btnEditProfile');
    const previewContainer = document.getElementById('previewContainer');

    if (enable) {
        originalName = inputName.value;
        originalPhone = inputPhone.value;
        inputName.removeAttribute('readonly');
        inputPhone.removeAttribute('readonly');
        inputName.focus();
        photoGroup.classList.remove('hidden');
        actionsDiv.classList.remove('hidden');
        editBtn.classList.add('hidden');
    } else {
        inputName.value = originalName;
        inputPhone.value = originalPhone;
        inputName.setAttribute('readonly', true);
        inputPhone.setAttribute('readonly', true);
        photoGroup.classList.add('hidden');
        actionsDiv.classList.add('hidden');
        editBtn.classList.remove('hidden');

        document.querySelector('input[type="file"]').value = '';
        previewContainer.classList.add('hidden');
    }
}

// 3. STOP REFRESH & VALIDATE PASSWORD
document.getElementById('btnUpdatePass').addEventListener('click', function (e) {
    const currentPass = document.getElementById('CurrentPassword').value;
    const newPass = document.getElementById('NewPassword').value;
    const confirmPass = document.getElementById('ConfirmNewPassword').value;

    const errCurrent = document.getElementById('err-current');
    const errNew = document.getElementById('err-new');
    const errConfirm = document.getElementById('err-confirm');

    // Clear previous errors
    errCurrent.innerText = "";
    errNew.innerText = "";
    errConfirm.innerText = "";

    let isValid = true;

    // If any password field is filled, enforce rules
    if (currentPass || newPass || confirmPass) {
        if (!currentPass) { errCurrent.innerText = "Required."; isValid = false; }
        if (!newPass) { errNew.innerText = "Required."; isValid = false; }
        if (!confirmPass) { errConfirm.innerText = "Required."; isValid = false; }

        if (newPass && confirmPass && newPass !== confirmPass) {
            errConfirm.innerText = "Passwords do not match.";
            isValid = false;
        }

        if (newPass && newPass.length < 5) {
            errNew.innerText = "Too short (min 5 chars).";
            isValid = false;
        }
    } else {
        e.preventDefault();
        return false;
    }

    // Prevent submission if invalid
    if (!isValid) {
        e.preventDefault();
    }
});

