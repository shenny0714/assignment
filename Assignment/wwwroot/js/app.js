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