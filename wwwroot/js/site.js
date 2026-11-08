/**
 * Stockify - Global SweetAlert2 & Loading System
 */
window.StockifyAlert = {
    // 1. Toast Notification (top-end corner, auto-dismiss)
    toast: function (icon, message) {
        if (typeof Swal === 'undefined') {
            console.log(icon, message);
            return;
        }
        const Toast = Swal.mixin({
            toast: true,
            position: 'top-end',
            showConfirmButton: false,
            timer: 3000,
            timerProgressBar: true,
            didOpen: (toast) => {
                toast.addEventListener('mouseenter', Swal.stopTimer);
                toast.addEventListener('mouseleave', Swal.resumeTimer);
            }
        });
        return Toast.fire({
            icon: icon || 'info',
            title: message
        });
    },

    toastSuccess: function (msg) {
        return this.toast('success', msg);
    },
    toastError: function (msg) {
        return this.toast('error', msg);
    },
    toastWarning: function (msg) {
        return this.toast('warning', msg);
    },
    toastInfo: function (msg) {
        return this.toast('info', msg);
    },

    // 2. Rich Dialog Popups
    success: function (title, text, callback) {
        if (typeof Swal === 'undefined') return alert(text);
        return Swal.fire({
            icon: 'success',
            title: title || 'Success!',
            text: text,
            confirmButtonText: 'OK',
            customClass: {
                confirmButton: 'btn btn-primary px-4'
            },
            buttonsStyling: false
        }).then((result) => {
            if (callback && result.isConfirmed) callback();
        });
    },

    error: function (title, text) {
        if (typeof Swal === 'undefined') return alert(text);
        return Swal.fire({
            icon: 'error',
            title: title || 'Error!',
            text: text,
            confirmButtonText: 'Close',
            customClass: {
                confirmButton: 'btn btn-danger px-4'
            },
            buttonsStyling: false
        });
    },

    warning: function (title, text) {
        if (typeof Swal === 'undefined') return alert(text);
        return Swal.fire({
            icon: 'warning',
            title: title || 'Warning!',
            text: text,
            confirmButtonText: 'Understand',
            customClass: {
                confirmButton: 'btn btn-warning px-4'
            },
            buttonsStyling: false
        });
    },

    info: function (title, text) {
        if (typeof Swal === 'undefined') return alert(text);
        return Swal.fire({
            icon: 'info',
            title: title || 'Information',
            text: text,
            confirmButtonText: 'OK',
            customClass: {
                confirmButton: 'btn btn-info px-4'
            },
            buttonsStyling: false
        });
    },

    // 3. Confirmation Dialog for Deletions & Irreversible Operations
    confirmDelete: function (title, text, onConfirmCallback) {
        if (typeof Swal === 'undefined') {
            if (confirm(text || 'Are you sure you want to delete this?')) {
                if (onConfirmCallback) onConfirmCallback();
            }
            return;
        }
        return Swal.fire({
            title: title || 'Are you sure?',
            text: text || "You won't be able to revert this operation!",
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: '<i class="bi bi-trash me-1"></i> Yes, Delete',
            cancelButtonText: 'Cancel',
            customClass: {
                confirmButton: 'btn btn-danger px-4 me-2',
                cancelButton: 'btn btn-secondary px-4'
            },
            buttonsStyling: false,
            reverseButtons: true
        }).then((result) => {
            if (result.isConfirmed) {
                if (onConfirmCallback) onConfirmCallback();
            }
        });
    },

    // 4. Loading indicator for database queries, asynchronous fetches & processing
    showLoading: function (title, text) {
        if (typeof Swal === 'undefined') return;
        return Swal.fire({
            title: title || 'Loading...',
            html: `
                <div class="py-3 text-center">
                    <div class="spinner-border text-primary mb-3" style="width: 3.5rem; height: 3.5rem;" role="status">
                        <span class="visually-hidden">Loading...</span>
                    </div>
                    <div class="text-muted fw-semibold small">${text || 'Connecting to database, please wait...'}</div>
                </div>
            `,
            showConfirmButton: false,
            allowOutsideClick: false,
            allowEscapeKey: false
        });
    },

    hideLoading: function () {
        if (typeof Swal !== 'undefined') {
            Swal.close();
        }
    }
};

document.addEventListener('DOMContentLoaded', function () {
    // 1. Unified Desktop & Mobile Sidebar Toggle (Open / Close) with Persistence
    const sidebarToggleBtn = document.getElementById('sidebarToggleBtn');
    const sidebarInternalCloseBtn = document.getElementById('sidebarInternalCloseBtn');
    const appSidebar = document.getElementById('appSidebar');
    const sidebarOverlay = document.getElementById('sidebarOverlay');

    const isDesktop = () => window.innerWidth >= 992;

    // Apply saved state on desktop (open by default, remember if user closed it)
    if (isDesktop()) {
        try {
            const isClosedSaved = localStorage.getItem('roven_sidebar_closed') === 'true';
            if (isClosedSaved) {
                document.body.classList.add('sidebar-closed');
            }
        } catch (e) {}
    }

    function toggleSidebar() {
        if (isDesktop()) {
            const isClosed = document.body.classList.toggle('sidebar-closed');
            try {
                localStorage.setItem('roven_sidebar_closed', isClosed ? 'true' : 'false');
            } catch (e) {}
        } else {
            if (appSidebar) {
                const isOpen = appSidebar.classList.toggle('show');
                if (sidebarOverlay) sidebarOverlay.classList.toggle('show', isOpen);
            }
        }
    }

    function closeSidebar() {
        if (isDesktop()) {
            document.body.classList.add('sidebar-closed');
            try {
                localStorage.setItem('roven_sidebar_closed', 'true');
            } catch (e) {}
        } else {
            if (appSidebar) {
                appSidebar.classList.remove('show');
                if (sidebarOverlay) sidebarOverlay.classList.remove('show');
            }
        }
    }

    if (sidebarToggleBtn) {
        sidebarToggleBtn.addEventListener('click', function (e) {
            e.preventDefault();
            toggleSidebar();
        });
    }

    if (sidebarInternalCloseBtn) {
        sidebarInternalCloseBtn.addEventListener('click', function (e) {
            e.preventDefault();
            closeSidebar();
        });
    }

    if (sidebarOverlay) {
        sidebarOverlay.addEventListener('click', function () {
            closeSidebar();
        });
    }

    // Keyboard shortcut (Ctrl + B or Alt + S) to toggle sidebar effortlessly
    document.addEventListener('keydown', function (e) {
        if ((e.ctrlKey && e.key.toLowerCase() === 'b') || (e.altKey && e.key.toLowerCase() === 's')) {
            e.preventDefault();
            toggleSidebar();
        }
    });

    // 2. Show / Hide Password Toggle
    const passwordToggleButtons = document.querySelectorAll('.password-toggle-btn');
    passwordToggleButtons.forEach(button => {
        button.addEventListener('click', function () {
            const targetSelector = this.getAttribute('data-target');
            const targetInput = document.querySelector(targetSelector);
            const icon = this.querySelector('i');

            if (targetInput) {
                if (targetInput.type === 'password') {
                    targetInput.type = 'text';
                    if (icon) {
                        icon.classList.remove('bi-eye');
                        icon.classList.add('bi-eye-slash');
                    }
                } else {
                    targetInput.type = 'password';
                    if (icon) {
                        icon.classList.remove('bi-eye-slash');
                        icon.classList.add('bi-eye');
                    }
                }
            }
        });
    });

    // 3. Functional Interactive Table Pagination & Search
    initDynamicTablePagination();

    // 4. Initialize Bootstrap Tooltips
    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });

    // 5. Global Delete Confirmation Listener for Tables
    document.addEventListener('click', function (e) {
        const deleteBtn = e.target.closest('.btn-delete, button.text-danger:has(.bi-trash), a.text-danger:has(.bi-trash)');
        if (deleteBtn && !deleteBtn.classList.contains('no-alert') && !deleteBtn.classList.contains('btn-remove-row')) {
            e.preventDefault();
            const itemName = deleteBtn.getAttribute('data-name') || 'this record';
            StockifyAlert.confirmDelete(
                'Confirm Deletion',
                `Are you sure you want to delete ${itemName}? This record will be permanently deleted from the database.`,
                function () {
                    const form = deleteBtn.closest('form');
                    if (form) {
                        StockifyAlert.showLoading('Deleting...', 'Removing record from database...');
                        form.submit();
                    } else if (deleteBtn.tagName === 'A' && deleteBtn.href && !deleteBtn.href.startsWith('javascript:')) {
                        StockifyAlert.showLoading('Deleting...', 'Removing record from database...');
                        window.location.href = deleteBtn.href;
                    } else {
                        const tr = deleteBtn.closest('tr');
                        if (tr) {
                            tr.remove();
                            StockifyAlert.toastSuccess(`${itemName} deleted successfully!`);
                        }
                    }
                }
            );
        }
    });

    // 6. Global Form Submission Loading Spinner
    document.addEventListener('submit', function (e) {
        const form = e.target;
        if (form && !form.classList.contains('no-loading') && form.method.toLowerCase() === 'post') {
            const submitBtn = form.querySelector('button[type="submit"]');
            if (submitBtn) {
                submitBtn.disabled = true;
            }
            StockifyAlert.showLoading('Saving Data...', 'Processing transaction and updating database...');
        }
    });
});

/**
 * Initializes real, interactive client-side pagination and search for tables
 */
function initDynamicTablePagination() {
    const tableCards = document.querySelectorAll('.card');

    tableCards.forEach(card => {
        const table = card.querySelector('table');
        const paginationNav = card.querySelector('.table-pagination-nav');
        if (!table || !paginationNav) return;

        const tbody = table.querySelector('tbody');
        if (!tbody) return;

        const rows = Array.from(tbody.querySelectorAll('tr'));
        if (rows.length === 0) return;

        const searchInput = card.querySelector('input[type="text"]');
        const pageSize = 3; // 3 rows per page so pagination is visibly active with sample data
        let currentPage = 1;
        let filteredRows = [...rows];

        function renderPagination() {
            const total = filteredRows.length;
            const totalPages = Math.ceil(total / pageSize) || 1;

            if (currentPage > totalPages) currentPage = totalPages;
            if (currentPage < 1) currentPage = 1;

            const startIndex = (currentPage - 1) * pageSize;
            const endIndex = Math.min(startIndex + pageSize, total);

            // Hide all rows first
            rows.forEach(r => r.style.display = 'none');

            // Show current page rows
            for (let i = startIndex; i < endIndex; i++) {
                if (filteredRows[i]) {
                    filteredRows[i].style.display = '';
                }
            }

            // Update info text
            const startSpan = paginationNav.querySelector('.start-entry');
            const endSpan = paginationNav.querySelector('.end-entry');
            const totalSpan = paginationNav.querySelector('.total-entries');

            if (startSpan) startSpan.textContent = total === 0 ? 0 : startIndex + 1;
            if (endSpan) endSpan.textContent = endIndex;
            if (totalSpan) totalSpan.textContent = total;

            // Render pagination buttons
            const controls = paginationNav.querySelector('.table-pagination-controls');
            if (controls) {
                controls.innerHTML = '';

                // Previous Button
                const prevLi = document.createElement('li');
                prevLi.className = `page-item ${currentPage === 1 ? 'disabled' : ''}`;
                prevLi.innerHTML = `<a class="page-link" href="javascript:void(0)" aria-label="Previous"><svg width="12" height="12" viewBox="0 0 16 16" fill="currentColor"><path fill-rule="evenodd" d="M11.354 1.646a.5.5 0 0 1 0 .708L5.707 8l5.647 5.646a.5.5 0 0 1-.708.708l-6-6a.5.5 0 0 1 0-.708l6-6a.5.5 0 0 1 .708 0z"/></svg></a>`;
                prevLi.addEventListener('click', () => {
                    if (currentPage > 1) {
                        currentPage--;
                        renderPagination();
                    }
                });
                controls.appendChild(prevLi);

                // Page Number Buttons
                for (let p = 1; p <= totalPages; p++) {
                    const pageLi = document.createElement('li');
                    pageLi.className = `page-item ${p === currentPage ? 'active' : ''}`;
                    pageLi.innerHTML = `<a class="page-link" href="javascript:void(0)">${p}</a>`;
                    pageLi.addEventListener('click', () => {
                        currentPage = p;
                        renderPagination();
                    });
                    controls.appendChild(pageLi);
                }

                // Next Button
                const nextLi = document.createElement('li');
                nextLi.className = `page-item ${currentPage === totalPages ? 'disabled' : ''}`;
                nextLi.innerHTML = `<a class="page-link" href="javascript:void(0)" aria-label="Next"><svg width="12" height="12" viewBox="0 0 16 16" fill="currentColor"><path fill-rule="evenodd" d="M4.646 1.646a.5.5 0 0 1 .708 0l6 6a.5.5 0 0 1 0 .708l-6 6a.5.5 0 0 1-.708-.708L10.293 8 4.646 2.354a.5.5 0 0 1 0-.708z"/></svg></a>`;
                nextLi.addEventListener('click', () => {
                    if (currentPage < totalPages) {
                        currentPage++;
                        renderPagination();
                    }
                });
                controls.appendChild(nextLi);
            }
        }

        // Live Search Filtering
        if (searchInput) {
            searchInput.addEventListener('input', function () {
                const query = this.value.trim().toLowerCase();
                if (!query) {
                    filteredRows = [...rows];
                } else {
                    filteredRows = rows.filter(row => {
                        return row.textContent.toLowerCase().includes(query);
                    });
                }
                currentPage = 1;
                renderPagination();
            });
        }

        // Initial render
        renderPagination();
    });
}
