// Smart Event Management — site.js

// Auto-dismiss toasts
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.toast').forEach(function (toastEl) {
        var toast = new bootstrap.Toast(toastEl, { delay: 4500 });
        toast.show();
    });

    // Sidebar toggle on mobile
    var toggleBtn = document.getElementById('sidebar-toggle');
    var sidebar = document.querySelector('.app-sidebar');
    if (toggleBtn && sidebar) {
        toggleBtn.addEventListener('click', function () {
            sidebar.classList.toggle('open');
        });
        document.addEventListener('click', function (e) {
            if (sidebar.classList.contains('open') &&
                !sidebar.contains(e.target) &&
                !toggleBtn.contains(e.target)) {
                sidebar.classList.remove('open');
            }
        });
    }

    // Mark active sidebar link
    var currentPath = window.location.pathname.toLowerCase();
    document.querySelectorAll('.sidebar-nav .nav-link').forEach(function (link) {
        if (link.getAttribute('href') &&
            currentPath.startsWith(link.getAttribute('href').toLowerCase())) {
            link.classList.add('active');
        }
    });

    // Submit button loading — only when the form will actually navigate away
    document.querySelectorAll('form').forEach(function (form) {
        form.addEventListener('submit', function () {
            // checkValidity covers HTML5 required/pattern checks.
            // jQuery Unobtrusive Validation adds its own check — we read it if present.
            var jqValid = true;
            if (window.jQuery && window.jQuery.validator) {
                var validator = window.jQuery(form).data('validator');
                if (validator && !window.jQuery(form).valid()) {
                    jqValid = false;
                }
            }

            if (!form.checkValidity() || !jqValid) return;

            var btn = form.querySelector('button[type="submit"]');
            if (!btn || btn.disabled) return;

            var original = btn.innerHTML;
            btn.disabled = true;
            btn.innerHTML =
                '<span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span> Please wait…';

            // Safety: re-enable after 12 s in case page stays (server validation error)
            setTimeout(function () {
                btn.disabled = false;
                btn.innerHTML = original;
            }, 12000);
        });
    });

    // Capacity progress bar colour
    document.querySelectorAll('.capacity-bar .progress-bar').forEach(function (bar) {
        var pct = parseInt(bar.style.width || bar.getAttribute('aria-valuenow') || 0);
        if (pct >= 90)      bar.classList.add('bg-danger');
        else if (pct >= 60) bar.classList.add('bg-warning');
        else                bar.classList.add('bg-success');
    });

    // Delete confirmation via data attribute
    document.querySelectorAll('[data-confirm]').forEach(function (el) {
        el.addEventListener('click', function (e) {
            if (!confirm(el.getAttribute('data-confirm'))) e.preventDefault();
        });
    });
});

// SignalR — real-time notifications
function initSignalR(userId) {
    if (typeof signalR === 'undefined') return;
    var connection = new signalR.HubConnectionBuilder()
        .withUrl('/notificationHub')
        .withAutomaticReconnect()
        .build();

    connection.on('ReceiveNotification', function (data) {
        showLiveToast(data.message, data.type || 'info');
    });

    connection.on('ReceiveSupportReply', function (data) {
        if (typeof window.onSupportReply === 'function') {
            window.onSupportReply(data);
        }
        showLiveToast('Your support question has been answered!', 'approval');
    });

    connection.on('ReceiveNewSupportQuery', function () {
        adjustSupportBadge(1);
    });

    connection.on('SupportQueryAnswered', function () {
        adjustSupportBadge(-1);
    });

    connection.start().then(function () {
        if (userId) connection.invoke('JoinUserGroup', userId).catch(console.error);
    }).catch(console.error);
}

function showLiveToast(message, type) {
    var colours = {
        approval: 'text-bg-success',
        cancellation: 'text-bg-danger',
        reminder: 'text-bg-warning',
        update: 'text-bg-primary',
        info: 'text-bg-info'
    };
    var cls = colours[type] || 'text-bg-info';
    var id = 'toast-' + Date.now();
    var html = '<div id="' + id + '" class="toast align-items-center ' + cls + ' border-0" role="alert">' +
        '<div class="d-flex">' +
        '<div class="toast-body"><i class="fa fa-bell me-2"></i>' + message + '</div>' +
        '<button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>' +
        '</div></div>';

    var container = document.getElementById('signalr-toast-container');
    if (container) {
        container.insertAdjacentHTML('beforeend', html);
        var toastEl = document.getElementById(id);
        var toast = new bootstrap.Toast(toastEl, { delay: 5000 });
        toast.show();
        toastEl.addEventListener('hidden.bs.toast', function () { toastEl.remove(); });
    }
}

function adjustSupportBadge(delta) {
    var badge = document.getElementById('support-query-badge');
    if (!badge) return;
    var count = (parseInt(badge.getAttribute('data-count') || '0', 10)) + delta;
    if (count < 0) count = 0;
    badge.setAttribute('data-count', count);
    badge.style.display = count > 0 ? 'flex' : 'none';
    badge.textContent = count > 99 ? '99+' : count;
}

// Chart helpers
function renderBarChart(canvasId, labels, data, label) {
    var ctx = document.getElementById(canvasId);
    if (!ctx) return;
    new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [{
                label: label || 'Count',
                data: data,
                backgroundColor: 'rgba(108,99,255,0.75)',
                borderRadius: 8,
                borderSkipped: false
            }]
        },
        options: {
            responsive: true,
            plugins: { legend: { display: false } },
            scales: {
                y: { beginAtZero: true, grid: { color: '#f0f0f8' }, ticks: { stepSize: 1 } },
                x: { grid: { display: false } }
            }
        }
    });
}

function renderDoughnutChart(canvasId, labels, data) {
    var ctx = document.getElementById(canvasId);
    if (!ctx) return;
    new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: labels,
            datasets: [{
                data: data,
                backgroundColor: [
                    '#6C63FF','#FF6584','#FF9A3C','#43E97B','#4facfe','#f7971e','#a18cd1'
                ],
                borderWidth: 0,
                hoverOffset: 8
            }]
        },
        options: {
            responsive: true,
            cutout: '65%',
            plugins: { legend: { position: 'right', labels: { font: { family: 'Poppins', size: 12 } } } }
        }
    });
}
