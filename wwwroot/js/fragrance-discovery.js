/**
 * MAISON ROVEN — FIND YOUR SCENT
 * Interactive Fragrance Discovery Quiz Controller
 */
(function () {
    'use strict';

    // Core Quiz State
    const quizState = {
        direction: null,          // "ForHer" | "ForHim" | "Unisex"
        lovedNoteIds: new Set(),  // Set of note IDs (numbers)
        avoidedNoteIds: new Set(),// Set of note IDs (numbers)
        seasons: new Set(),       // Set of "Spring", "Summer", "Fall", "Winter", "AllYear"
        projection: null          // "Moderate" | "Strong" | "Enormous"
    };

    let currentStep = 1;
    const totalSteps = 5;
    let catalogData = null;
    let isDataLoaded = false;
    let isLoadingData = false;
    let isRecommending = false;

    // DOM Elements
    const elements = {
        floatingBtn: document.getElementById('rovenFindScentFloatingBtn'),
        backdrop: document.getElementById('rovenQuizBackdrop'),
        modal: document.getElementById('rovenQuizModal'),
        closeBtn: document.getElementById('rovenQuizCloseBtn'),
        stepLabel: document.getElementById('rovenQuizStepLabel'),
        progressText: document.getElementById('rovenQuizProgressText'),
        progressFill: document.getElementById('rovenQuizProgressFill'),
        prevBtn: document.getElementById('rovenQuizPrevBtn'),
        nextBtn: document.getElementById('rovenQuizNextBtn'),
        nextBtnText: document.getElementById('rovenQuizNextBtnText'),
        nextBtnIcon: document.getElementById('rovenQuizNextBtnIcon'),
        skipBtn: document.getElementById('rovenQuizSkipBtn'),

        // Panels
        panels: {
            1: document.getElementById('rovenStepPanel1'),
            2: document.getElementById('rovenStepPanel2'),
            3: document.getElementById('rovenStepPanel3'),
            4: document.getElementById('rovenStepPanel4'),
            5: document.getElementById('rovenStepPanel5'),
            complete: document.getElementById('rovenStepPanelComplete'),
            results: document.getElementById('rovenStepPanelResults')
        },

        // Phase 5 Results Elements
        recommendationsContainer: document.getElementById('rovenRecommendationsContainer'),
        emptyResultsContainer: document.getElementById('rovenEmptyResultsContainer'),
        errorStateContainer: document.getElementById('rovenErrorStateContainer'),
        errorMessage: document.getElementById('rovenErrorMessage'),
        resultsFilterSummary: document.getElementById('rovenResultsFilterSummary'),
        resultsEditPrefBtn: document.getElementById('rovenResultsEditPrefBtn'),
        emptyEditBtn: document.getElementById('rovenEmptyEditBtn'),
        emptyResetBtn: document.getElementById('rovenEmptyResetBtn'),
        retryCalculationBtn: document.getElementById('rovenRetryCalculationBtn'),
        retakeFooterBtn: document.getElementById('rovenQuizRetakeFooterBtn'),
        closeFooterBtn: document.getElementById('rovenQuizCloseFooterBtn'),

        // Step 2 & 3 Containers
        lovedCountBadge: document.getElementById('rovenLovedCountBadge'),
        lovedQuickPicks: document.getElementById('rovenLovedQuickPicksContainer'),
        lovedCategories: document.getElementById('rovenLovedCategoriesContainer'),
        lovedSearchInput: document.getElementById('rovenLovedSearchInput'),
        lovedClearSearch: document.getElementById('rovenLovedClearSearchBtn'),
        lovedConflictAlert: document.getElementById('rovenLovedConflictAlert'),
        lovedConflictMsg: document.getElementById('rovenLovedConflictMsg'),

        avoidedCountBadge: document.getElementById('rovenAvoidedCountBadge'),
        avoidedQuickPicks: document.getElementById('rovenAvoidedQuickPicksContainer'),
        avoidedCategories: document.getElementById('rovenAvoidedCategoriesContainer'),
        avoidedSearchInput: document.getElementById('rovenAvoidedSearchInput'),
        avoidedClearSearch: document.getElementById('rovenAvoidedClearSearchBtn'),
        avoidedConflictAlert: document.getElementById('rovenAvoidedConflictAlert'),
        avoidedConflictMsg: document.getElementById('rovenAvoidedConflictMsg'),

        summaryBadge: document.getElementById('rovenQuizSummaryBadge')
    };

    // Initialize when DOM is ready
    document.addEventListener('DOMContentLoaded', initQuiz);

    function initQuiz() {
        if (!elements.floatingBtn || !elements.backdrop) return;

        attachModalEvents();
        attachStep1Events();
        attachStep4Events();
        attachStep5Events();
        attachNavEvents();
        attachSearchEvents();
        attachResultsEvents();

        // Load cached state from sessionStorage if available
        restoreSavedState();

        // Preload catalog data in background
        fetchCatalogData();

        // Auto-collapse floating CTA to search icon after 5 seconds
        initFloatingBtnAutoCollapse();
    }

    function initFloatingBtnAutoCollapse() {
        if (!elements.floatingBtn) return;

        let isHovered = false;
        let shouldCollapse = false;

        elements.floatingBtn.addEventListener('mouseenter', function () {
            isHovered = true;
        });

        elements.floatingBtn.addEventListener('mouseleave', function () {
            isHovered = false;
            if (shouldCollapse) {
                elements.floatingBtn.classList.add('collapsed');
            }
        });

        setTimeout(function () {
            shouldCollapse = true;
            if (!isHovered) {
                elements.floatingBtn.classList.add('collapsed');
            }
        }, 5000);
    }

    // ==========================================================================
    // MODAL CONTROL & ACCESSIBILITY
    // ==========================================================================
    function attachModalEvents() {
        elements.floatingBtn.addEventListener('click', openModal);
        elements.closeBtn.addEventListener('click', closeModal);

        // Click outside modal to close
        elements.backdrop.addEventListener('click', function (e) {
            if (e.target === elements.backdrop) {
                closeModal();
            }
        });

        // Prevent background page scrolling when wheeling or touching on backdrop
        elements.backdrop.addEventListener('wheel', function (e) {
            if (!elements.modal.contains(e.target)) {
                e.preventDefault();
            }
        }, { passive: false });

        elements.backdrop.addEventListener('touchmove', function (e) {
            if (!elements.modal.contains(e.target)) {
                e.preventDefault();
            }
        }, { passive: false });

        // Prevent scroll chaining inside modal when reaching top/bottom boundaries
        if (elements.modal) {
            elements.modal.addEventListener('wheel', function (e) {
                const scrollable = e.target.closest('.roven-quiz-body, .roven-quiz-dropdown, .roven-notes-group-body');
                if (!scrollable) {
                    e.preventDefault();
                    return;
                }
                const delta = e.deltaY;
                const atTop = scrollable.scrollTop <= 0;
                const atBottom = scrollable.scrollHeight - scrollable.scrollTop <= scrollable.clientHeight + 1;
                if ((delta < 0 && atTop) || (delta > 0 && atBottom)) {
                    e.preventDefault();
                }
            }, { passive: false });
        }

        // ESC key to close
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && elements.backdrop.classList.contains('active')) {
                closeModal();
            }
        });
    }

    function openModal() {
        elements.backdrop.classList.add('active');
        document.documentElement.classList.add('roven-quiz-open');
        document.body.classList.add('roven-quiz-open');
        document.documentElement.style.overflow = 'hidden';
        document.body.style.overflow = 'hidden';

        if (!isDataLoaded) {
            fetchCatalogData();
        }

        renderStep(currentStep);
    }

    function closeModal() {
        elements.backdrop.classList.remove('active');
        document.documentElement.classList.remove('roven-quiz-open');
        document.body.classList.remove('roven-quiz-open');
        document.documentElement.style.overflow = '';
        document.body.style.overflow = '';
        saveState();
    }

    // ==========================================================================
    // DATA FETCHING & RENDERING
    // ==========================================================================
    async function fetchCatalogData() {
        if (isDataLoaded || isLoadingData) return;
        isLoadingData = true;

        try {
            const resp = await fetch('/api/fragrance-discovery/quiz-data');
            if (!resp.ok) throw new Error('Network response was not ok');
            catalogData = await resp.json();
            isDataLoaded = true;

            renderNotesInterface('loved');
            renderNotesInterface('avoided');
        } catch (err) {
            console.error('Failed to load fragrance discovery data:', err);
            if (elements.lovedQuickPicks) {
                elements.lovedQuickPicks.innerHTML = '<div class="text-danger small py-2">Unable to load fragrance notes. Please try again.</div>';
            }
            if (elements.avoidedQuickPicks) {
                elements.avoidedQuickPicks.innerHTML = '<div class="text-danger small py-2">Unable to load fragrance notes. Please try again.</div>';
            }
        } finally {
            isLoadingData = false;
        }
    }

    function renderNotesInterface(type) {
        if (!catalogData) return;

        const isLoved = type === 'loved';
        const quickPicksContainer = isLoved ? elements.lovedQuickPicks : elements.avoidedQuickPicks;
        const categoriesContainer = isLoved ? elements.lovedCategories : elements.avoidedCategories;
        const quickPicksData = isLoved ? catalogData.lovedQuickPicks : catalogData.avoidedQuickPicks;
        const selectedSet = isLoved ? quizState.lovedNoteIds : quizState.avoidedNoteIds;

        // 1. Render Quick Picks
        if (quickPicksContainer) {
            quickPicksContainer.innerHTML = '';
            if (quickPicksData && quickPicksData.length > 0) {
                quickPicksData.forEach(qp => {
                    const isSelected = selectedSet.has(qp.fragranceNoteId);
                    const pill = createNotePill(qp.fragranceNoteId, qp.name, qp.arabicName, isLoved, isSelected);
                    quickPicksContainer.appendChild(pill);
                });
            } else {
                quickPicksContainer.innerHTML = '<div class="text-muted small py-1">No quick picks configured.</div>';
            }
        }

        // 2. Render Categories Accordions
        if (categoriesContainer) {
            categoriesContainer.innerHTML = '';
            catalogData.categories.forEach((cat, idx) => {
                const catNotes = catalogData.notes.filter(n => n.category === cat.id);
                if (catNotes.length === 0) return;

                const selectedInCat = catNotes.filter(n => selectedSet.has(n.id)).length;
                const accordionItem = document.createElement('div');
                accordionItem.className = 'roven-cat-accordion-item';
                accordionItem.setAttribute('data-category-id', cat.id);

                const collapseId = `rovenCatCollapse_${type}_${cat.id}`;

                accordionItem.innerHTML = `
                    <button type="button" class="roven-cat-accordion-btn" data-target="#${collapseId}">
                        <div class="roven-cat-title-wrap">
                            <i class="fa-solid ${cat.icon}" style="color: ${cat.color};"></i>
                            <span>${cat.nameEn}</span>
                        </div>
                        <span class="roven-cat-badge ${selectedInCat > 0 ? 'has-selected' : ''}" data-cat-badge="${cat.id}">
                            ${selectedInCat > 0 ? `${selectedInCat} selected` : `${catNotes.length} notes`}
                        </span>
                    </button>
                    <div id="${collapseId}" class="roven-cat-accordion-body ${idx === 0 ? '' : 'd-none'}">
                        <div class="roven-notes-pill-cloud"></div>
                    </div>
                `;

                const cloud = accordionItem.querySelector('.roven-notes-pill-cloud');
                catNotes.forEach(n => {
                    const isSelected = selectedSet.has(n.id);
                    const pill = createNotePill(n.id, n.name, n.arabicName, isLoved, isSelected);
                    cloud.appendChild(pill);
                });

                // Accordion Toggle
                const btn = accordionItem.querySelector('.roven-cat-accordion-btn');
                const body = accordionItem.querySelector('.roven-cat-accordion-body');
                btn.addEventListener('click', function () {
                    const isHidden = body.classList.contains('d-none');
                    body.classList.toggle('d-none', !isHidden);
                });

                categoriesContainer.appendChild(accordionItem);
            });
        }

        updateSelectionCounters(type);
    }

    function createNotePill(id, nameEn, nameAr, isLoved, isSelected) {
        const pill = document.createElement('div');
        pill.className = `roven-note-pill ${isSelected ? (isLoved ? 'selected' : 'selected-avoid') : ''}`;
        pill.setAttribute('data-note-id', id);
        pill.setAttribute('data-note-en', nameEn.toLowerCase());
        pill.setAttribute('data-note-ar', nameAr);

        pill.innerHTML = `
            <i class="fa-solid fa-check note-check-icon"></i>
            <span>${nameEn}</span>
        `;

        pill.addEventListener('click', function () {
            handleNoteClick(id, nameEn, nameAr, isLoved);
        });

        return pill;
    }

    // ==========================================================================
    // CONFLICT RESOLUTION (LOVED VS AVOIDED)
    // ==========================================================================
    function handleNoteClick(id, nameEn, nameAr, isLoved) {
        const currentTargetSet = isLoved ? quizState.lovedNoteIds : quizState.avoidedNoteIds;
        const opposingSet = isLoved ? quizState.avoidedNoteIds : quizState.lovedNoteIds;
        const opposingType = isLoved ? 'avoided' : 'loved';
        const targetType = isLoved ? 'loved' : 'avoided';

        const isCurrentlySelected = currentTargetSet.has(id);

        if (isCurrentlySelected) {
            // Deselect
            currentTargetSet.delete(id);
        } else {
            // Check conflict
            if (opposingSet.has(id)) {
                opposingSet.delete(id);
                syncPillVisuals(id, opposingType, false);
                updateSelectionCounters(opposingType);

                // Show conflict notification
                showConflictAlert(targetType, `Note "${nameEn}" was in your ${opposingType} notes and has been moved to ${targetType} notes.`);
            }
            currentTargetSet.add(id);
        }

        syncPillVisuals(id, targetType, !isCurrentlySelected);
        updateSelectionCounters(targetType);
        saveState();
    }

    function syncPillVisuals(id, type, isSelected) {
        const selector = `.roven-note-pill[data-note-id="${id}"]`;
        const container = type === 'loved' ? elements.panels[2] : elements.panels[3];
        if (!container) return;

        const pills = container.querySelectorAll(selector);
        pills.forEach(pill => {
            const activeClass = type === 'loved' ? 'selected' : 'selected-avoid';
            pill.classList.toggle(activeClass, isSelected);
        });
    }

    function updateSelectionCounters(type) {
        const isLoved = type === 'loved';
        const selectedSet = isLoved ? quizState.lovedNoteIds : quizState.avoidedNoteIds;
        const badge = isLoved ? elements.lovedCountBadge : elements.avoidedCountBadge;
        const container = isLoved ? elements.lovedCategories : elements.avoidedCategories;

        if (badge) {
            badge.textContent = `${selectedSet.size} selected`;
        }

        // Update category badges
        if (container && catalogData) {
            const catItems = container.querySelectorAll('.roven-cat-accordion-item');
            catItems.forEach(item => {
                const catId = parseInt(item.getAttribute('data-category-id'), 10);
                const catNotes = catalogData.notes.filter(n => n.category === catId);
                const selectedInCat = catNotes.filter(n => selectedSet.has(n.id)).length;
                const catBadge = item.querySelector('.roven-cat-badge');

                if (catBadge) {
                    if (selectedInCat > 0) {
                        catBadge.textContent = `${selectedInCat} selected`;
                        catBadge.classList.add('has-selected');
                    } else {
                        catBadge.textContent = `${catNotes.length} notes`;
                        catBadge.classList.remove('has-selected');
                    }
                }
            });
        }
    }

    function showConflictAlert(type, msg) {
        const alertBox = type === 'loved' ? elements.lovedConflictAlert : elements.avoidedConflictAlert;
        const msgEl = type === 'loved' ? elements.lovedConflictMsg : elements.avoidedConflictMsg;

        if (alertBox && msgEl) {
            msgEl.textContent = msg;
            alertBox.classList.remove('d-none');
            setTimeout(() => {
                alertBox.classList.add('d-none');
            }, 4500);
        }
    }

    // ==========================================================================
    // STEP 1: DIRECTION EVENTS
    // ==========================================================================
    function attachStep1Events() {
        const cards = elements.panels[1].querySelectorAll('.roven-direction-card');
        cards.forEach(card => {
            card.addEventListener('click', function () {
                cards.forEach(c => c.classList.remove('selected'));
                card.classList.add('selected');
                quizState.direction = card.getAttribute('data-direction');
                saveState();
            });
        });
    }

    // ==========================================================================
    // STEP 4: SEASONS (EXCLUSIVE ALL YEAR)
    // ==========================================================================
    function attachStep4Events() {
        const cards = elements.panels[4].querySelectorAll('.roven-season-card');
        const allYearCard = elements.panels[4].querySelector('.roven-season-card[data-is-all-year="true"]');
        const individualCards = Array.from(cards).filter(c => c.getAttribute('data-is-all-year') !== 'true');

        cards.forEach(card => {
            const season = card.getAttribute('data-season');
            const isAllYear = card.getAttribute('data-is-all-year') === 'true';

            card.addEventListener('click', function () {
                const isSelected = card.classList.contains('selected');

                if (isAllYear) {
                    if (!isSelected) {
                        // Clear all individual seasons
                        individualCards.forEach(c => {
                            c.classList.remove('selected');
                            quizState.seasons.delete(c.getAttribute('data-season'));
                        });
                        card.classList.add('selected');
                        quizState.seasons.clear();
                        quizState.seasons.add('AllYear');
                    } else {
                        card.classList.remove('selected');
                        quizState.seasons.delete('AllYear');
                    }
                } else {
                    if (!isSelected) {
                        // Clear All Year
                        if (allYearCard) {
                            allYearCard.classList.remove('selected');
                            quizState.seasons.delete('AllYear');
                        }
                        card.classList.add('selected');
                        quizState.seasons.add(season);
                    } else {
                        card.classList.remove('selected');
                        quizState.seasons.delete(season);
                    }
                }
                saveState();
            });
        });
    }

    // ==========================================================================
    // STEP 5: PROJECTION
    // ==========================================================================
    function attachStep5Events() {
        const cards = elements.panels[5].querySelectorAll('.roven-projection-card');
        cards.forEach(card => {
            card.addEventListener('click', function () {
                cards.forEach(c => c.classList.remove('selected'));
                card.classList.add('selected');
                quizState.projection = card.getAttribute('data-projection');
                saveState();
            });
        });
    }

    // ==========================================================================
    // SEARCH INPUTS
    // ==========================================================================
    function attachSearchEvents() {
        setupFilter(elements.lovedSearchInput, elements.lovedClearSearch, elements.panels[2]);
        setupFilter(elements.avoidedSearchInput, elements.avoidedClearSearch, elements.panels[3]);

        function setupFilter(input, clearBtn, panel) {
            if (!input) return;

            input.addEventListener('input', function () {
                const q = this.value.trim().toLowerCase();
                if (clearBtn) {
                    clearBtn.classList.toggle('d-none', q.length === 0);
                }

                const pills = panel.querySelectorAll('.roven-note-pill');
                pills.forEach(pill => {
                    const en = pill.getAttribute('data-note-en') || '';
                    const ar = pill.getAttribute('data-note-ar') || '';
                    const match = !q || en.includes(q) || ar.includes(q);
                    pill.classList.toggle('d-none', !match);
                });

                // Auto-open accordions when searching
                if (q.length > 0) {
                    panel.querySelectorAll('.roven-cat-accordion-body').forEach(body => {
                        const hasVisible = body.querySelectorAll('.roven-note-pill:not(.d-none)').length > 0;
                        if (hasVisible) {
                            body.classList.remove('d-none');
                        }
                    });
                }
            });

            if (clearBtn) {
                clearBtn.addEventListener('click', function () {
                    input.value = '';
                    input.dispatchEvent(new Event('input'));
                    input.focus();
                });
            }
        }
    }

    // ==========================================================================
    // NAVIGATION & STEP PROGRESSION
    // ==========================================================================
    function attachNavEvents() {
        elements.nextBtn.addEventListener('click', handleNextClick);
        elements.prevBtn.addEventListener('click', handlePrevClick);
        elements.skipBtn.addEventListener('click', handleSkipClick);
    }

    function handleNextClick() {
        if (!validateCurrentStep()) return;

        if (currentStep < totalSteps) {
            currentStep++;
            renderStep(currentStep);
        } else {
            // Step 5 completed -> Complete Quiz
            completeQuiz();
        }
    }

    function handlePrevClick() {
        if (currentStep > 1) {
            currentStep--;
            renderStep(currentStep);
        }
    }

    function handleSkipClick() {
        if (currentStep === 2 || currentStep === 3) {
            currentStep++;
            renderStep(currentStep);
        }
    }

    function validateCurrentStep() {
        if (currentStep === 1) {
            if (!quizState.direction) {
                highlightValidation(elements.panels[1], 'Please select who this fragrance is for before proceeding.');
                return false;
            }
        } else if (currentStep === 4) {
            if (quizState.seasons.size === 0) {
                highlightValidation(elements.panels[4], 'Please choose at least one season or All Year.');
                return false;
            }
        } else if (currentStep === 5) {
            if (!quizState.projection) {
                highlightValidation(elements.panels[5], 'Please select your preferred projection intensity.');
                return false;
            }
        }
        return true;
    }

    function highlightValidation(panel, msg) {
        let alert = panel.querySelector('.roven-step-validation-msg');
        if (!alert) {
            alert = document.createElement('div');
            alert.className = 'alert alert-danger py-2 px-3 small border-0 shadow-xs mb-3 roven-step-validation-msg';
            panel.insertBefore(alert, panel.firstChild);
        }
        alert.innerHTML = `<i class="fa-solid fa-circle-exclamation me-1.5"></i>${msg}`;
        alert.scrollIntoView({ behavior: 'smooth', block: 'nearest' });

        setTimeout(() => {
            if (alert) alert.remove();
        }, 3500);
    }

    function renderStep(step) {
        // Toggle step panels
        Object.keys(elements.panels).forEach(key => {
            if (elements.panels[key]) {
                elements.panels[key].classList.toggle('active', key === step.toString());
            }
        });

        // Update step label & progress bar
        const percent = Math.round((step / totalSteps) * 100);
        if (elements.stepLabel) elements.stepLabel.textContent = `Step ${step} of ${totalSteps}`;
        if (elements.progressText) elements.progressText.textContent = `${percent}% Complete`;
        if (elements.progressFill) elements.progressFill.style.width = `${percent}%`;

        // Update footer buttons
        if (elements.nextBtn) {
            elements.nextBtn.classList.remove('d-none');
        }
        if (elements.retakeFooterBtn) elements.retakeFooterBtn.classList.add('d-none');
        if (elements.closeFooterBtn) elements.closeFooterBtn.classList.add('d-none');

        if (elements.prevBtn) {
            elements.prevBtn.classList.toggle('d-none', step === 1);
        }

        if (elements.skipBtn) {
            // Optional steps: 2 and 3
            elements.skipBtn.classList.toggle('d-none', !(step === 2 || step === 3));
            elements.skipBtn.textContent = step === 2 ? 'Skip to Disliked Notes' : 'Skip to Seasons';
        }

        if (elements.nextBtnText && elements.nextBtnIcon) {
            if (step === totalSteps) {
                elements.nextBtnText.textContent = 'Find My Scent';
                elements.nextBtnIcon.className = 'fa-solid fa-arrow-right ms-1';
            } else {
                elements.nextBtnText.textContent = 'Next';
                elements.nextBtnIcon.className = 'fa-solid fa-arrow-right ms-1';
            }
        }

        // Scroll modal body to top
        const body = document.querySelector('.roven-quiz-body');
        if (body) body.scrollTop = 0;
    }

    async function completeQuiz() {
        if (isRecommending) return; // Prevent duplicate submissions
        isRecommending = true;
        saveState();

        // Render loading / bridge view
        Object.keys(elements.panels).forEach(key => {
            if (elements.panels[key]) {
                elements.panels[key].classList.remove('active');
            }
        });
        if (elements.panels.complete) {
            elements.panels.complete.classList.add('active');
        }

        // Hide navigation buttons
        if (elements.prevBtn) elements.prevBtn.classList.add('d-none');
        if (elements.nextBtn) elements.nextBtn.classList.add('d-none');
        if (elements.skipBtn) elements.skipBtn.classList.add('d-none');
        if (elements.stepLabel) elements.stepLabel.textContent = 'Quiz Complete';
        if (elements.progressText) elements.progressText.textContent = '100% Complete';
        if (elements.progressFill) elements.progressFill.style.width = '100%';

        // Summary details badge
        if (elements.summaryBadge) {
            const dir = quizState.direction;
            const lovedCount = quizState.lovedNoteIds.size;
            const avoidCount = quizState.avoidedNoteIds.size;
            const seasonsArr = Array.from(quizState.seasons).join(', ');
            const proj = quizState.projection;

            elements.summaryBadge.innerHTML = `
                <i class="fa-solid fa-spinner fa-spin text-primary me-1"></i>
                Connecting with atelier recommendation engine...
            `;
        }

        // Build Payload
        const quizPayload = {
            direction: quizState.direction,
            lovedNoteIds: Array.from(quizState.lovedNoteIds),
            avoidedNoteIds: Array.from(quizState.avoidedNoteIds),
            seasons: Array.from(quizState.seasons),
            projection: quizState.projection,
            completedAt: new Date().toISOString()
        };

        // Dispatch completed event for external listeners
        window.dispatchEvent(new CustomEvent('rovenQuizCompleted', { detail: quizPayload }));
        console.log('ROVEN Find Your Scent — Customer Quiz Answers payload:', quizPayload);

        // Call backend Recommendation API
        try {
            const resp = await fetch('/api/fragrance-discovery/recommendations', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(quizPayload)
            });

            if (!resp.ok) {
                const errData = await resp.json().catch(() => null);
                throw new Error(errData?.message || 'Server error computing recommendations');
            }

            const recData = await resp.json();
            console.log('ROVEN Recommendations received from Phase 4 engine:', recData);

            // Cache recommendations in sessionStorage
            try {
                sessionStorage.setItem('roven_quiz_recommendations', JSON.stringify(recData));
            } catch (e) {
                // ignore storage quota error
            }

            // Update loading summary badge with real database results
            if (elements.summaryBadge) {
                const matches = recData.totalMatchesFound || 0;
                const total = recData.totalEligibleProducts || 0;
                elements.summaryBadge.innerHTML = `
                    <i class="fa-solid fa-check-circle text-success me-1"></i>
                    Analysis complete: <strong>${matches}</strong> matching fragrance${matches !== 1 ? 's' : ''} identified across <strong>${total}</strong> atelier compositions.
                `;
            }

            // Dispatch event for Phase 5 listeners
            window.dispatchEvent(new CustomEvent('rovenRecommendationsReceived', { detail: recData }));

            // Transition from loading screen into Phase 5 Results Screen
            setTimeout(() => {
                renderResultsScreen(recData);
            }, 500);
        } catch (err) {
            console.error('Failed to get recommendations:', err);
            renderErrorScreen(err.message || 'Unable to load recommendations. Please try again.');
        } finally {
            isRecommending = false;
        }
    }

    // ==========================================================================
    // PHASE 5: RESULTS SCREEN RENDERING & CARD COMPONENT
    // ==========================================================================
    function renderResultsScreen(recData) {
        // 1. Hide all step & loading panels
        Object.keys(elements.panels).forEach(key => {
            if (elements.panels[key]) {
                elements.panels[key].classList.remove('active');
            }
        });

        // 2. Activate results panel
        if (elements.panels.results) {
            elements.panels.results.classList.add('active');
        }

        // 3. Update Header
        if (elements.stepLabel) elements.stepLabel.textContent = 'QUIZ COMPLETE';
        if (elements.progressText) elements.progressText.textContent = '100% Complete';
        if (elements.progressFill) elements.progressFill.style.width = '100%';

        // 4. Update Footer buttons
        if (elements.prevBtn) elements.prevBtn.classList.add('d-none');
        if (elements.nextBtn) elements.nextBtn.classList.add('d-none');
        if (elements.skipBtn) elements.skipBtn.classList.add('d-none');
        if (elements.retakeFooterBtn) elements.retakeFooterBtn.classList.remove('d-none');
        if (elements.closeFooterBtn) elements.closeFooterBtn.classList.remove('d-none');

        // Scroll modal body to top smoothly
        const body = document.querySelector('.roven-quiz-body');
        if (body) body.scrollTop = 0;

        // 5. Populate Filter Summary Pill
        if (elements.resultsFilterSummary) {
            const dir = quizState.direction === 'ForHim' ? 'For Him'
                      : quizState.direction === 'ForHer' ? 'For Her'
                      : 'Unisex';
            const lovedCount = quizState.lovedNoteIds.size;
            const seasons = Array.from(quizState.seasons).join(', ');
            const proj = quizState.projection || '';

            elements.resultsFilterSummary.innerHTML = `
                <i class="fa-solid fa-check text-success me-1"></i>
                ${dir} • ${lovedCount} Loved Notes • ${seasons} • ${proj}
            `;
        }

        const recs = recData.recommendations || [];

        // 6. Handle Empty vs Results
        if (recs.length === 0) {
            if (elements.recommendationsContainer) elements.recommendationsContainer.classList.add('d-none');
            if (elements.emptyResultsContainer) elements.emptyResultsContainer.classList.remove('d-none');
            if (elements.errorStateContainer) elements.errorStateContainer.classList.add('d-none');
        } else {
            if (elements.emptyResultsContainer) elements.emptyResultsContainer.classList.add('d-none');
            if (elements.errorStateContainer) elements.errorStateContainer.classList.add('d-none');
            if (elements.recommendationsContainer) {
                elements.recommendationsContainer.classList.remove('d-none');
                elements.recommendationsContainer.innerHTML = recs.map((item, idx) => renderRecommendationCard(item, idx === 0)).join('');
                attachCardActions();
            }
        }
    }

    function renderRecommendationCard(item, isBestMatch) {
        const rankLabel = item.rank === 1 ? '#1 Best Match'
                        : item.rank === 2 ? '#2 Great Match'
                        : item.rank === 3 ? '#3 Good Match'
                        : `#${item.rank} Match`;

        const rankClass = item.rank === 1 ? 'rec-rank-best'
                        : item.rank === 2 ? 'rec-rank-great'
                        : item.rank === 3 ? 'rec-rank-good'
                        : 'rec-rank-standard';

        const rankIcon = item.rank === 1 ? 'fa-crown'
                       : item.rank === 2 ? 'fa-medal'
                       : item.rank === 3 ? 'fa-award'
                       : 'fa-star';

        // Audience target representation
        const userAud = quizState.direction;
        const audienceLabel = userAud === 'ForHim' ? 'For Him'
                            : userAud === 'ForHer' ? 'For Her'
                            : 'Unisex';
        const audienceIcon = userAud === 'ForHim' ? 'fa-mars'
                           : userAud === 'ForHer' ? 'fa-venus'
                           : 'fa-venus-mars';

        // Explanations bullets
        const explanationsHtml = (item.matchExplanations || []).map(exp => `
            <li>
                <i class="fa-solid fa-circle-check"></i>
                <span>${escapeHtml(exp)}</span>
            </li>
        `).join('');

        // Notes Chips
        const lovedSet = new Set(item.matchedLovedNotes || []);
        const notesChipsHtml = (item.allProductNotes || []).map(note => {
            const isMatched = lovedSet.has(note);
            return `
                <span class="rec-note-chip ${isMatched ? 'is-loved-match' : ''}">
                    ${isMatched ? '<i class="fa-solid fa-heart"></i>' : ''}
                    <span>${escapeHtml(note)}</span>
                </span>
            `;
        }).join('');

        const formattedPrice = Number(item.price).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        const imageUrl = item.imageUrl || '/images/default-perfume.jpg';
        const tagline = item.fragranceTagline || 'Extrait de Parfum • Maison ROVEN';
        const bottleSize = item.bottleSize || '50ml'; // Fixed ROVEN bottle size rule

        return `
            <div class="roven-recommendation-card ${isBestMatch ? 'is-best-match' : ''}" data-product-id="${item.productId}">
                <!-- Top Image Showcase -->
                <div class="rec-card-image-wrapper">
                    <img src="${imageUrl}" alt="${escapeHtml(item.productName)}" class="rec-card-img" loading="lazy" />
                    
                    <span class="rec-badge-audience">
                        <i class="fa-solid ${audienceIcon}"></i> ${audienceLabel}
                    </span>

                    <button type="button" class="rec-favorite-btn" aria-label="Save to favorites" title="Favorite">
                        <i class="fa-regular fa-heart"></i>
                    </button>

                    <div class="rec-match-badge">
                        <span class="rec-match-number">${item.matchPercentage}%</span>
                        <span class="rec-match-label">Match</span>
                    </div>
                </div>

                <!-- Card Content -->
                <div class="rec-card-content">
                    <div class="rec-rank-row">
                        <span class="rec-rank-badge ${rankClass}">
                            <i class="fa-solid ${rankIcon}"></i> ${rankLabel}
                        </span>
                        <span class="rec-bottle-size-badge">
                            <i class="fa-solid fa-flask-vial me-1"></i>${bottleSize} • Extrait
                        </span>
                    </div>

                    <h4 class="rec-product-name">${escapeHtml(item.productName)}</h4>
                    <p class="rec-product-tagline">${escapeHtml(tagline)}</p>

                    <div class="rec-price-row">
                        <div class="rec-price">
                            <span class="rec-price-currency">EGP</span>
                            <span class="rec-price-val">${formattedPrice}</span>
                        </div>
                        ${item.inspiredBy ? `
                            <div class="rec-inspired-by">
                                <span class="rec-inspired-label">inspired by </span>
                                <span class="rec-inspired-name">${escapeHtml(item.inspiredBy)}</span>
                            </div>
                        ` : ''}
                    </div>

                    <!-- WHY THIS MATCHES YOU Section -->
                    <div class="rec-section-box">
                        <div class="rec-section-title">
                            <i class="fa-solid fa-circle-check"></i>
                            <span>WHY THIS MATCHES YOU</span>
                        </div>
                        <ul class="rec-reasons-list">
                            ${explanationsHtml}
                        </ul>
                    </div>

                    <!-- KEY NOTES Section -->
                    <div class="rec-section-box">
                        <div class="rec-section-title">
                            <i class="fa-solid fa-feather"></i>
                            <span>KEY NOTES</span>
                        </div>
                        <div class="rec-notes-chips">
                            ${notesChipsHtml}
                        </div>
                    </div>

                    <!-- BUY NOW Button -->
                    <div class="rec-buy-action">
                        <button type="button" class="roven-btn-buy-now roven-buy-now-trigger" data-product-id="${item.productId}" data-product-name="${escapeHtml(item.productName)}">
                            <i class="fa-solid fa-bag-shopping me-2"></i>
                            <span>BUY NOW</span>
                        </button>
                    </div>
                </div>
            </div>
        `;
    }

    function renderErrorScreen(errorMessage) {
        Object.keys(elements.panels).forEach(key => {
            if (elements.panels[key]) elements.panels[key].classList.remove('active');
        });

        if (elements.panels.results) elements.panels.results.classList.add('active');
        if (elements.recommendationsContainer) elements.recommendationsContainer.classList.add('d-none');
        if (elements.emptyResultsContainer) elements.emptyResultsContainer.classList.add('d-none');
        if (elements.errorStateContainer) elements.errorStateContainer.classList.remove('d-none');
        if (elements.errorMessage) elements.errorMessage.textContent = errorMessage;
    }

    function attachCardActions() {
        if (!elements.recommendationsContainer) return;

        // 1. BUY NOW Handler
        const buyBtns = elements.recommendationsContainer.querySelectorAll('.roven-buy-now-trigger');
        buyBtns.forEach(btn => {
            btn.addEventListener('click', async function (e) {
                e.preventDefault();
                const productId = this.getAttribute('data-product-id');
                const productName = this.getAttribute('data-product-name');
                if (!productId || this.disabled) return;

                const origHtml = this.innerHTML;
                this.disabled = true;
                this.innerHTML = '<i class="fa-solid fa-spinner fa-spin me-2"></i>Adding to Bag...';

                try {
                    const formData = new FormData();
                    formData.append('productId', productId);
                    formData.append('quantity', 1);

                    const csrfToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
                    const reqHeaders = {};
                    if (csrfToken) {
                        reqHeaders['RequestVerificationToken'] = csrfToken;
                    }

                    const res = await fetch('/Cart/AddToCart', {
                        method: 'POST',
                        headers: reqHeaders,
                        body: formData
                    });

                    if (!res.ok) throw new Error('Network response was not ok');
                    const data = await res.json();

                    if (data.success) {
                        const badge = document.getElementById('cart-item-count');
                        if (badge && data.cartCount !== undefined) {
                            badge.textContent = data.cartCount;
                        }

                        this.classList.add('btn-added-state');
                        this.innerHTML = '<i class="fa-solid fa-check me-2"></i>Added to Bag!';

                        if (window.Swal) {
                            Swal.fire({
                                icon: 'success',
                                iconColor: '#420B0B',
                                title: 'Added to Bag!',
                                text: data.message || `${productName} has been added to your shopping bag.`,
                                toast: true,
                                position: 'top-end',
                                showConfirmButton: false,
                                timer: 2400,
                                timerProgressBar: true
                            });
                        }

                        setTimeout(() => {
                            this.classList.remove('btn-added-state');
                            this.innerHTML = origHtml;
                            this.disabled = false;
                        }, 1800);
                    } else {
                        throw new Error(data.message || 'Unable to add item to bag.');
                    }
                } catch (err) {
                    console.error('Cart add error:', err);
                    this.innerHTML = '<i class="fa-solid fa-triangle-exclamation me-1.5"></i>Error';
                    if (window.Swal) {
                        Swal.fire({
                            icon: 'error',
                            title: 'Notice',
                            text: err.message || 'Could not add to cart.',
                            toast: true,
                            position: 'top-end',
                            showConfirmButton: false,
                            timer: 3000
                        });
                    }
                    setTimeout(() => {
                        this.innerHTML = origHtml;
                        this.disabled = false;
                    }, 2000);
                }
            });
        });

        // 2. Favorite Toggle Handler
        const favBtns = elements.recommendationsContainer.querySelectorAll('.rec-favorite-btn');
        favBtns.forEach(btn => {
            btn.addEventListener('click', function () {
                this.classList.toggle('active');
                const icon = this.querySelector('i');
                if (this.classList.contains('active')) {
                    icon.className = 'fa-solid fa-heart';
                } else {
                    icon.className = 'fa-regular fa-heart';
                }
            });
        });
    }

    function attachResultsEvents() {
        // Edit Preferences buttons
        if (elements.resultsEditPrefBtn) {
            elements.resultsEditPrefBtn.addEventListener('click', handleEditPreferences);
        }
        if (elements.emptyEditBtn) {
            elements.emptyEditBtn.addEventListener('click', handleEditPreferences);
        }

        // Retake Quiz / Reset buttons
        if (elements.emptyResetBtn) {
            elements.emptyResetBtn.addEventListener('click', handleRetakeQuiz);
        }
        if (elements.retakeFooterBtn) {
            elements.retakeFooterBtn.addEventListener('click', handleRetakeQuiz);
        }
        if (elements.closeFooterBtn) {
            elements.closeFooterBtn.addEventListener('click', closeModal);
        }

        // Retry Analysis on error
        if (elements.retryCalculationBtn) {
            elements.retryCalculationBtn.addEventListener('click', completeQuiz);
        }
    }

    function handleEditPreferences() {
        currentStep = 1;
        renderStep(1);

        if (elements.panels.results) elements.panels.results.classList.remove('active');
        if (elements.retakeFooterBtn) elements.retakeFooterBtn.classList.add('d-none');
        if (elements.closeFooterBtn) elements.closeFooterBtn.classList.add('d-none');
    }

    function handleRetakeQuiz() {
        window.rovenQuiz.reset();
    }

    function escapeHtml(str) {
        if (!str) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    // ==========================================================================
    // LOCAL STORAGE PERSISTENCE
    // ==========================================================================
    function saveState() {
        const data = {
            direction: quizState.direction,
            lovedNoteIds: Array.from(quizState.lovedNoteIds),
            avoidedNoteIds: Array.from(quizState.avoidedNoteIds),
            seasons: Array.from(quizState.seasons),
            projection: quizState.projection,
            currentStep: currentStep
        };
        try {
            sessionStorage.setItem('roven_quiz_state', JSON.stringify(data));
        } catch (e) {
            // ignore storage errors
        }
    }

    function restoreSavedState() {
        try {
            const raw = sessionStorage.getItem('roven_quiz_state');
            if (!raw) return;
            const data = JSON.parse(raw);

            if (data.direction) {
                quizState.direction = data.direction;
                const card = elements.panels[1].querySelector(`.roven-direction-card[data-direction="${data.direction}"]`);
                if (card) card.classList.add('selected');
            }

            if (data.lovedNoteIds && Array.isArray(data.lovedNoteIds)) {
                quizState.lovedNoteIds = new Set(data.lovedNoteIds);
            }

            if (data.avoidedNoteIds && Array.isArray(data.avoidedNoteIds)) {
                quizState.avoidedNoteIds = new Set(data.avoidedNoteIds);
            }

            if (data.seasons && Array.isArray(data.seasons)) {
                quizState.seasons = new Set(data.seasons);
                elements.panels[4].querySelectorAll('.roven-season-card').forEach(c => {
                    const s = c.getAttribute('data-season');
                    if (quizState.seasons.has(s)) c.classList.add('selected');
                });
            }

            if (data.projection) {
                quizState.projection = data.projection;
                const card = elements.panels[5].querySelector(`.roven-projection-card[data-projection="${data.projection}"]`);
                if (card) card.classList.add('selected');
            }

            if (data.currentStep && data.currentStep >= 1 && data.currentStep <= totalSteps) {
                currentStep = data.currentStep;
            }
        } catch (e) {
            // ignore
        }
    }

    // Expose helpers globally for testing or integration
    window.rovenQuiz = {
        open: openModal,
        close: closeModal,
        getState: () => ({
            direction: quizState.direction,
            lovedNoteIds: Array.from(quizState.lovedNoteIds),
            avoidedNoteIds: Array.from(quizState.avoidedNoteIds),
            seasons: Array.from(quizState.seasons),
            projection: quizState.projection,
            currentStep: currentStep
        }),
        reset: () => {
            quizState.direction = null;
            quizState.lovedNoteIds.clear();
            quizState.avoidedNoteIds.clear();
            quizState.seasons.clear();
            quizState.projection = null;
            currentStep = 1;
            sessionStorage.removeItem('roven_quiz_state');
            sessionStorage.removeItem('roven_quiz_recommendations');
            document.querySelectorAll('.roven-direction-card, .roven-season-card, .roven-projection-card, .roven-note-pill').forEach(el => {
                el.classList.remove('selected', 'selected-avoid');
            });
            if (elements.panels.results) elements.panels.results.classList.remove('active');
            if (elements.retakeFooterBtn) elements.retakeFooterBtn.classList.add('d-none');
            if (elements.closeFooterBtn) elements.closeFooterBtn.classList.add('d-none');
            if (elements.recommendationsContainer) elements.recommendationsContainer.innerHTML = '';
            updateSelectionCounters('loved');
            updateSelectionCounters('avoided');
            renderStep(1);
        }
    };
})();
