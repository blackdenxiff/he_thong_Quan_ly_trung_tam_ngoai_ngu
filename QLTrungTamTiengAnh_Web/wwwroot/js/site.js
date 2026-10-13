$(function () {
    const sidebar = $('#sidebar-wrapper');
    const pageContent = $('#page-content-wrapper');
    const backdrop = $('#sidebar-backdrop');

    function closeMobileSidebar() {
        sidebar.removeClass('toggled');
        backdrop.removeClass('visible');
    }

    $('#menu-toggle').on('click', function (event) {
        event.preventDefault();

        if (window.innerWidth > 768) {
            sidebar.toggleClass('collapsed');
            pageContent.toggleClass('expanded');
        } else {
            sidebar.toggleClass('toggled');
            backdrop.toggleClass('visible', sidebar.hasClass('toggled'));
        }
    });

    $('#sidebar-close, #sidebar-backdrop').on('click', function () {
        closeMobileSidebar();
    });

    $('.sidebar-menu a').on('click', function () {
        if (window.innerWidth <= 768) {
            closeMobileSidebar();
        }
    });

    $(window).on('resize', function () {
        if (window.innerWidth > 768) {
            closeMobileSidebar();
        }
    });
});
