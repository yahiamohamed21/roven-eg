/**
 * ROVEN Fragrance Profile Management (Find Your Scent ✨)
 * Handles Audience selection, Exclusive All-Year Season logic,
 * Projection radio selection, and Categorized Fragrance Notes search & pills.
 */
document.addEventListener('DOMContentLoaded', function () {
    initAudiencePills();
    initSeasonPills();
    initProjectionRadios();
    initNotesPillsAndSearch();
});

function initAudiencePills() {
    const pillLabels = document.querySelectorAll('.audience-pill-btn');
    pillLabels.forEach(label => {
        const checkbox = label.querySelector('.audience-checkbox');
        if (!checkbox) return;

        label.addEventListener('click', function (e) {
            if (e.target !== checkbox) {
                e.preventDefault();
                checkbox.checked = !checkbox.checked;
            }
            updateAudienceStyle(label, checkbox.checked);
        });
    });

    function updateAudienceStyle(label, isChecked) {
        if (isChecked) {
            label.classList.add('active', 'btn-dark', 'text-white', 'border-dark');
            label.classList.remove('bg-white');
        } else {
            label.classList.remove('active', 'btn-dark', 'text-white', 'border-dark');
            label.classList.add('bg-white');
        }
    }
}

function initSeasonPills() {
    const pills = document.querySelectorAll('.season-pill-btn');
    const allYearPill = document.querySelector('.season-pill-btn[data-is-all-year="true"]');
    const allYearCheckbox = allYearPill ? allYearPill.querySelector('.season-checkbox') : null;
    const individualPills = Array.from(pills).filter(p => p.getAttribute('data-is-all-year') !== 'true');

    pills.forEach(label => {
        const checkbox = label.querySelector('.season-checkbox');
        const isAllYear = label.getAttribute('data-is-all-year') === 'true';
        if (!checkbox) return;

        label.addEventListener('click', function (e) {
            if (e.target !== checkbox) {
                e.preventDefault();
                checkbox.checked = !checkbox.checked;
            }

            if (isAllYear) {
                if (checkbox.checked) {
                    // Selecting All Year clears all other seasons
                    individualPills.forEach(p => {
                        const cb = p.querySelector('.season-checkbox');
                        if (cb) {
                            cb.checked = false;
                            updateSeasonStyle(p, false, false);
                        }
                    });
                }
                updateSeasonStyle(label, checkbox.checked, true);
            } else {
                if (checkbox.checked) {
                    // Selecting an individual season clears All Year
                    if (allYearCheckbox) {
                        allYearCheckbox.checked = false;
                        updateSeasonStyle(allYearPill, false, true);
                    }
                }
                updateSeasonStyle(label, checkbox.checked, false);
            }
        });
    });

    function updateSeasonStyle(label, isChecked, isAllYear) {
        if (!label) return;
        if (isChecked) {
            if (isAllYear) {
                label.classList.add('active', 'btn-warning', 'text-dark', 'border-warning');
                label.classList.remove('bg-white', 'btn-dark', 'text-white', 'border-dark');
            } else {
                label.classList.add('active', 'btn-dark', 'text-white', 'border-dark');
                label.classList.remove('bg-white', 'btn-warning', 'border-warning');
            }
        } else {
            label.classList.remove('active', 'btn-dark', 'text-white', 'border-dark', 'btn-warning', 'border-warning');
            label.classList.add('bg-white');
        }
    }
}

function initProjectionRadios() {
    const cards = document.querySelectorAll('.projection-card');
    cards.forEach(card => {
        const radio = card.querySelector('.projection-radio');
        if (!radio) return;

        card.addEventListener('click', function (e) {
            if (e.target !== radio) {
                radio.checked = true;
            }
            refreshProjectionCards();
        });

        radio.addEventListener('change', refreshProjectionCards);
    });

    function refreshProjectionCards() {
        cards.forEach(c => {
            const r = c.querySelector('.projection-radio');
            if (r && r.checked) {
                c.classList.add('border-dark', 'bg-dark', 'text-white');
                c.classList.remove('bg-white');
            } else {
                c.classList.remove('border-dark', 'bg-dark', 'text-white');
                c.classList.add('bg-white');
            }
        });
    }
}

function initNotesPillsAndSearch() {
    const searchInput = document.getElementById('fragranceNoteSearchInput');
    const clearSearchBtn = document.getElementById('btnClearNoteSearch');
    const notePillButtons = document.querySelectorAll('.note-pill-btn');
    const countBadge = document.getElementById('selectedNotesCount');
    const clearAllBtn = document.getElementById('btnClearAllNotes');

    notePillButtons.forEach(btn => {
        const checkbox = btn.querySelector('.note-checkbox');
        if (!checkbox) return;

        btn.addEventListener('click', function (e) {
            if (e.target !== checkbox) {
                e.preventDefault();
                checkbox.checked = !checkbox.checked;
            }
            updateNotePillStyle(btn, checkbox.checked);
            updateCounters();
        });
    });

    function updateNotePillStyle(btn, isChecked) {
        if (isChecked) {
            btn.classList.add('active', 'btn-primary', 'text-white', 'border-primary');
            btn.classList.remove('bg-white', 'text-dark');
        } else {
            btn.classList.remove('active', 'btn-primary', 'text-white', 'border-primary');
            btn.classList.add('bg-white', 'text-dark');
        }
    }

    function updateCounters() {
        const checkedCheckboxes = document.querySelectorAll('.note-checkbox:checked');
        const count = checkedCheckboxes.length;
        if (countBadge) countBadge.textContent = count;
        if (clearAllBtn) {
            if (count > 0) clearAllBtn.classList.remove('d-none');
            else clearAllBtn.classList.add('d-none');
        }

        // Update each category accordion counter
        const groups = document.querySelectorAll('.category-accordion-group');
        groups.forEach(g => {
            const catId = g.getAttribute('data-category');
            const totalInCat = g.querySelectorAll('.note-checkbox').length;
            const checkedInCat = g.querySelectorAll('.note-checkbox:checked').length;
            const catCounter = g.querySelector('.cat-selected-counter');
            if (catCounter) {
                if (checkedInCat > 0) {
                    catCounter.textContent = `${checkedInCat} selected`;
                    catCounter.classList.add('bg-primary');
                    catCounter.classList.remove('bg-secondary', 'bg-opacity-25', 'text-dark');
                } else {
                    catCounter.textContent = `${totalInCat} notes`;
                    catCounter.classList.remove('bg-primary');
                    catCounter.classList.add('bg-secondary', 'bg-opacity-25', 'text-dark');
                }
            }
        });
    }

    // Search filter across English and Arabic names
    if (searchInput) {
        searchInput.addEventListener('input', function () {
            const query = this.value.trim().toLowerCase();
            if (clearSearchBtn) {
                if (query.length > 0) clearSearchBtn.classList.remove('d-none');
                else clearSearchBtn.classList.add('d-none');
            }

            let visibleCount = 0;
            notePillButtons.forEach(btn => {
                const en = btn.getAttribute('data-note-name-en') || '';
                const ar = btn.getAttribute('data-note-name-ar') || '';
                const matches = !query || en.includes(query) || ar.includes(query);

                if (matches) {
                    btn.classList.remove('d-none');
                    visibleCount++;
                } else {
                    btn.classList.add('d-none');
                }
            });

            // If searching, ensure accordion items with visible notes stay open
            if (query.length > 0) {
                document.querySelectorAll('.accordion-collapse').forEach(c => {
                    const hasVisible = c.querySelectorAll('.note-pill-btn:not(.d-none)').length > 0;
                    if (hasVisible) {
                        c.classList.add('show');
                    }
                });
            }
        });

        if (clearSearchBtn) {
            clearSearchBtn.addEventListener('click', function () {
                searchInput.value = '';
                searchInput.dispatchEvent(new Event('input'));
                searchInput.focus();
            });
        }
    }

    if (clearAllBtn) {
        clearAllBtn.addEventListener('click', function () {
            if (confirm('Deselect all fragrance notes?')) {
                notePillButtons.forEach(btn => {
                    const cb = btn.querySelector('.note-checkbox');
                    if (cb) {
                        cb.checked = false;
                        updateNotePillStyle(btn, false);
                    }
                });
                updateCounters();
            }
        });
    }
}
