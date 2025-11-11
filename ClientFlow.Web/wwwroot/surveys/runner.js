    (function () {
        const qs = new URLSearchParams(location.search);
        const code = qs.get('code');
        const titleEl = document.getElementById('title');
        const descEl = document.getElementById('desc');
        const barEl = document.getElementById('bar');
        const sectionHost = document.getElementById('section');
        const msgEl = document.getElementById('msg');
        const prevBtn = document.getElementById('prev');
        const nextBtn = document.getElementById('next');
        const submitBtn = document.getElementById('submit');

        const STYLE_SCOPE = '.runner-shell';

        if (!code) {
            showMessage('Survey code missing from the URL.', 'error');
            disableControls();
            return;
        }

        let definition = null;
        let sections = [];
        let questions = [];
        let rules = [];
        let pageIndex = 0;
        let sessionStarted = Date.now();
        const answers = Object.create(null);

        const parseSettings = (json) => {
            if (!json) return {};
            try { return JSON.parse(json); }
            catch { return {}; }
        };

        function scopeCss(css, scopeSel) {
            if (!css) return '';
            const parts = css.split('}');
            const out = [];
            for (const part of parts) {
                if (!part.trim()) continue;
                const idx = part.indexOf('{');
                if (idx === -1) continue;
                const sel = part.slice(0, idx).trim();
                const body = part.slice(idx + 1);
                if (sel.startsWith('@media')) {
                    out.push(sel + '{' + scopeCss(body, scopeSel) + '}');
                } else if (sel.startsWith('@keyframes') || sel.startsWith('@font-face')) {
                    out.push(sel + '{' + body + '}');
                } else {
                    const scoped = sel.split(',').map(s => {
                        s = s.trim();
                        if (/^:root\b/.test(s) || /^html\b/.test(s) || /^body\b/.test(s)) return scopeSel;
                        return scopeSel + ' ' + s;
                    }).join(', ');
                    out.push(scoped + '{' + body + '}');
                }
            }
            return out.join('\n');
        }

        function applyCustomCss(rawCss) {
            if (!rawCss) return;
            const scopedCss = scopeCss(rawCss, STYLE_SCOPE);
            let tag = document.getElementById('runnerScopedStyle');
            if (!tag) {
                tag = document.createElement('style');
                tag.id = 'runnerScopedStyle';
                document.head.appendChild(tag);
            }
            tag.textContent = scopedCss;
        }

        function disableControls() {
            prevBtn.disabled = true;
            nextBtn.disabled = true;
            submitBtn.disabled = true;
        }

        function showMessage(text, kind = 'error') {
            msgEl.textContent = text;
            msgEl.classList.remove('hidden', 'error', 'success');
            msgEl.classList.add('alert', kind === 'success' ? 'success' : 'error');
        }

        function clearMessage() {
            msgEl.textContent = '';
            msgEl.classList.add('hidden');
            msgEl.classList.remove('error', 'success');
        }

        function sectionQuestions(sectionId) {
            return questions.filter(q => (q.sectionId ?? null) === (sectionId ?? null));
        }

        function evaluateCondition(expr) {
            if (!expr) return true;
            const equals = (key, val) => (answers[key] ?? null) == val;
            const inRange = (key, min, max) => {
                const v = Number(answers[key]);
                if (Number.isNaN(v)) return false;
                return v >= min && v <= max;
            };
            const selected = (key, val) => {
                const current = answers[key];
                if (Array.isArray(current)) return current.includes(val);
                return current == val;
            };
            try {
                return Function('equals', 'inRange', 'selected', 'answers', `return (${expr});`)(equals, inRange, selected, answers);
            } catch {
                return false;
            }
        }

        function questionVisible(q) {
            if (!q.visibleIf) return true;
            return evaluateCondition(q.visibleIf);
        }

        function questionOptions(q) {
            const opts = Array.isArray(q.options) ? q.options.slice() : [];
            return opts.sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
        }

        function ensureDefaults() {
            for (const q of questions) {
                const settings = parseSettings(q.settingsJson);
                if (settings && settings.defaultValue != null && answers[q.key] == null) {
                    answers[q.key] = String(settings.defaultValue);
                }
            }
        }

        function updateProgress() {
            const total = sections.length;
            const pct = total === 0 ? 0 : ((pageIndex + 1) / total) * 100;
            barEl.style.width = pct.toFixed(2) + '%';
        }

        function render() {
            clearMessage();
            if (!sections.length) {
                sectionHost.innerHTML = '<div class="message-card">No sections available for this survey.</div>';
                disableControls();
                return;
            }

            const section = sections[Math.max(0, Math.min(pageIndex, sections.length - 1))];
            pageIndex = sections.indexOf(section);

            sectionHost.innerHTML = '';
            const header = document.createElement('div');
            if (section.title) {
                header.className = 'section-title';
                header.textContent = section.title;
                sectionHost.appendChild(header);
            }

            const fieldsWrap = document.createElement('div');
            fieldsWrap.className = 'fields';
            if (section.columns && section.columns > 1) {
                fieldsWrap.style.gridTemplateColumns = `repeat(${Math.min(2, section.columns)}, minmax(0, 1fr))`;
            }
            sectionHost.appendChild(fieldsWrap);

            const secQuestions = sectionQuestions(section.id).filter(questionVisible);
            for (const q of secQuestions) {
                renderQuestion(fieldsWrap, q);
            }

            updateProgress();
            prevBtn.classList.toggle('hidden', pageIndex === 0);
            const isLast = pageIndex === sections.length - 1;
            nextBtn.classList.toggle('hidden', isLast);
            submitBtn.classList.toggle('hidden', !isLast);
            prevBtn.disabled = pageIndex === 0;
            nextBtn.disabled = false;
            submitBtn.disabled = false;
        }

        function renderQuestion(container, q) {
            const type = (q.type || '').trim().toLowerCase();
            const settings = parseSettings(q.settingsJson);
            const field = document.createElement('div');
            field.className = 'field';

            if (type === 'message' || type === 'static_text' || type === 'static-html' || type === 'static_html') {
                const note = document.createElement('div');
                note.className = 'message-card';
                if ((type === 'static-html' || type === 'static_html') && settings.html) {
                    note.innerHTML = settings.html;
                } else {
                    const textSetting = typeof settings.text === 'string' ? settings.text.trim() : '';
                    const message = textSetting || q.prompt || '';
                    note.textContent = message;
                }
                field.appendChild(note);
                container.appendChild(field);
                return;
            }

            if (type === 'image') {
                const url = (settings.url || '').trim();
                if (!url) {
                    const placeholder = document.createElement('div');
                    placeholder.className = 'message-card';
                    placeholder.textContent = 'Image not available.';
                    field.appendChild(placeholder);
                    container.appendChild(field);
                    return;
                }
                const media = document.createElement('div');
                media.className = 'media-wrapper';
                const img = document.createElement('img');
                img.src = url;
                img.alt = settings.alt || q.prompt || '';
                if (settings.width) img.style.width = settings.width;
                if (settings.height) img.style.height = settings.height;
                media.appendChild(img);
                if (settings.caption) {
                    const caption = document.createElement('div');
                    caption.className = 'media-caption';
                    caption.textContent = settings.caption;
                    media.appendChild(caption);
                }
                field.appendChild(media);
                container.appendChild(field);
                return;
            }

            if (type === 'video') {
                const pickFirstNonEmpty = (...values) => {
                    for (const value of values) {
                        if (typeof value === 'string') {
                            const trimmed = value.trim();
                            if (trimmed) return trimmed;
                        }
                    }
                    return '';
                };
                const mediaSource = pickFirstNonEmpty(settings.embedHtml, settings.html, settings.url);
                if (!mediaSource) {
                    const placeholder = document.createElement('div');
                    placeholder.className = 'message-card';
                    placeholder.textContent = 'Video not available.';
                    field.appendChild(placeholder);
                    container.appendChild(field);
                    return;
                }

                const media = document.createElement('div');
                media.className = 'media-wrapper';

                const looksLikeHtml = mediaSource.startsWith('<');
                const looksLikeVideoFile = /\.(mp4|webm|ogg)(\?.*)?$/i.test(mediaSource) || mediaSource.startsWith('blob:') || mediaSource.startsWith('data:video');
                let mediaElement = null;

                if (looksLikeHtml) {
                    const template = document.createElement('template');
                    template.innerHTML = mediaSource;
                    const iframe = template.content.querySelector('iframe');
                    if (iframe) {
                        iframe.setAttribute('frameborder', '0');
                        iframe.setAttribute('allowfullscreen', '');
                        iframe.setAttribute('loading', 'lazy');
                        if (!iframe.hasAttribute('allow')) {
                            iframe.setAttribute('allow', 'accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share');
                        }
                        if (!iframe.style.width) iframe.style.width = '100%';
                        if (!iframe.style.height) iframe.style.height = '360px';
                        mediaElement = iframe;
                    } else {
                        const inlineVideo = template.content.querySelector('video');
                        if (inlineVideo) {
                            inlineVideo.playsInline = true;
                            if (!inlineVideo.hasAttribute('controls')) inlineVideo.controls = true;
                            if (settings.autoplay && !inlineVideo.autoplay) {
                                inlineVideo.autoplay = true;
                                inlineVideo.muted = true;
                                inlineVideo.setAttribute('muted', '');
                            }
                            if (settings.loop && !inlineVideo.loop) inlineVideo.loop = true;
                            if (settings.poster && !inlineVideo.poster) inlineVideo.poster = settings.poster;
                            mediaElement = inlineVideo;
                        } else {
                            const first = template.content.firstElementChild;
                            if (first) {
                                mediaElement = first;
                            }
                        }
                    }
                }

                if (!mediaElement && !looksLikeHtml && !looksLikeVideoFile) {
                    const iframe = document.createElement('iframe');
                    iframe.src = mediaSource;
                    iframe.setAttribute('frameborder', '0');
                    iframe.setAttribute('allowfullscreen', '');
                    iframe.setAttribute('loading', 'lazy');
                    iframe.setAttribute('allow', 'accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share');
                    mediaElement = iframe;
                }

                if (!mediaElement) {
                    const video = document.createElement('video');
                    video.controls = true;
                    video.playsInline = true;
                    video.preload = 'metadata';
                    if (settings.autoplay) {
                        video.autoplay = true;
                        video.muted = true;
                        video.setAttribute('muted', '');
                    }
                    if (settings.loop) video.loop = true;
                    if (settings.poster) video.poster = settings.poster;
                    if (looksLikeHtml) {
                        const template = document.createElement('template');
                        template.innerHTML = mediaSource;
                        video.append(...template.content.childNodes);
                    } else {
                        const sourceEl = document.createElement('source');
                        sourceEl.src = mediaSource;
                        video.appendChild(sourceEl);
                    }
                    mediaElement = video;
                }

                media.appendChild(mediaElement);
                if (settings.caption) {
                    const caption = document.createElement('div');
                    caption.className = 'media-caption';
                    caption.textContent = settings.caption;
                    media.appendChild(caption);
                }
                field.appendChild(media);
                container.appendChild(field);
                return;
            }

            if (type === 'divider') {
                const hr = document.createElement('hr');
                hr.className = 'divider-rule';
                if (settings.color) hr.style.borderTopColor = settings.color;
                if (settings.thickness) hr.style.borderTopWidth = settings.thickness + 'px';
                if (settings.style) hr.style.borderTopStyle = settings.style;
                field.appendChild(hr);
                container.appendChild(field);
                return;
            }

            if (type === 'spacer') {
                const spacer = document.createElement('div');
                spacer.className = 'spacer-block';
                const sizeMap = { sm: 16, md: 28, lg: 44 };
                const customHeight = Number(settings.customHeight);
                const height = Number.isFinite(customHeight) && customHeight > 0
                    ? customHeight
                    : sizeMap[settings.size] ?? 28;
                spacer.style.height = `${height}px`;
                field.appendChild(spacer);
                container.appendChild(field);
                return;
            }

            const label = document.createElement('label');
            label.textContent = q.prompt + (q.required ? ' *' : '');
            field.appendChild(label);

            const currentValue = answers[q.key];
            let control = null;

            switch (type) {
                case 'nps':
                case 'nps_0_10':
                case 'nps-0-10': {
                    control = renderNpsControl(q, currentValue);
                    break;
                }
                case 'rating':
                case 'rating_stars':
                case 'rating-stars': {
                    const maxStars = Number(settings.stars) > 0 ? Number(settings.stars) : 5;
                    control = renderRatingControl(q, currentValue, maxStars);
                    break;
                }
                case 'yesno':
                case 'single':
                case 'single-select':
                case 'singlechoice':
                case 'radio': {
                    control = renderOptionTiles(q, currentValue);
                    break;
                }
                case 'dropdown':
                case 'select': {
                    control = document.createElement('select');
                    for (const opt of questionOptions(q)) {
                        const optionEl = document.createElement('option');
                        optionEl.value = opt.value;
                        optionEl.textContent = opt.label;
                        if (currentValue === opt.value) optionEl.selected = true;
                        control.appendChild(optionEl);
                    }
                    control.addEventListener('change', () => {
                        answers[q.key] = control.value;
                    });
                    break;
                }
                case 'multi':
                case 'multi-select':
                case 'checkbox': {
                    control = renderMultiSelect(q, currentValue);
                    break;
                }
                case 'textarea': {
                    control = document.createElement('textarea');
                    if (settings.placeholder) control.placeholder = settings.placeholder;
                    control.value = currentValue ?? '';
                    control.addEventListener('input', () => {
                        answers[q.key] = control.value;
                    });
                    break;
                }
                case 'number': {
                    control = document.createElement('input');
                    control.type = 'number';
                    if (settings.min != null) control.min = settings.min;
                    if (settings.max != null) control.max = settings.max;
                    if (settings.step != null) control.step = settings.step;
                    control.value = currentValue ?? '';
                    control.addEventListener('input', () => {
                        answers[q.key] = control.value;
                    });
                    break;
                }
                case 'date': {
                    control = document.createElement('input');
                    control.type = 'date';
                    control.value = currentValue ?? '';
                    if (settings.min) control.min = settings.min;
                    if (settings.max) control.max = settings.max;
                    control.addEventListener('input', () => {
                        answers[q.key] = control.value;
                    });
                    break;
                }
                case 'phone':
                case 'email':
                case 'text':
                default: {
                    control = document.createElement('input');
                    if (type === 'phone') control.type = 'tel';
                    else if (type === 'email') control.type = 'email';
                    else control.type = 'text';
                    if (settings.placeholder) control.placeholder = settings.placeholder;
                    control.value = currentValue ?? '';
                    control.addEventListener('input', () => {
                        answers[q.key] = control.value;
                    });
                    break;
                }
            }

            if (control) {
                if (q.required && ('value' in control || control instanceof HTMLTextAreaElement)) {
                    control.required = true;
                }
                field.appendChild(control);
            }

            container.appendChild(field);
        }

        function renderOptionTiles(question, currentValue) {
            const wrap = document.createElement('div');
            wrap.className = 'option-grid';
            const opts = questionOptions(question);
            const columns = question.settingsJson ? parseSettings(question.settingsJson).columns : null;
            if (columns && columns > 1) {
                wrap.style.gridTemplateColumns = `repeat(${columns}, minmax(0, 1fr))`;
            } else {
                wrap.style.gridTemplateColumns = `repeat(auto-fit, minmax(180px, 1fr))`;
            }
            const updateSelection = () => {
                wrap.querySelectorAll('.option-tile').forEach(tile => {
                    tile.classList.toggle('selected', tile.dataset.value === String(answers[question.key] ?? ''));
                });
            };
            for (const opt of opts) {
                const tile = document.createElement('button');
                tile.type = 'button';
                tile.className = 'option-tile';
                tile.dataset.value = opt.value;
                tile.textContent = opt.label;
                tile.addEventListener('click', () => {
                    answers[question.key] = opt.value;
                    updateSelection();
                });
                if (currentValue === opt.value) tile.classList.add('selected');
                wrap.appendChild(tile);
            }
            updateSelection();
            return wrap;
        }

        function renderMultiSelect(question, currentValue) {
            const wrap = document.createElement('div');
            wrap.className = 'option-grid';
            wrap.style.gridTemplateColumns = 'repeat(auto-fit, minmax(180px, 1fr))';
            const selected = Array.isArray(currentValue) ? currentValue.slice() : (typeof currentValue === 'string' && currentValue.length ? currentValue.split(',') : []);
            const toggle = (value) => {
                const idx = selected.indexOf(value);
                if (idx >= 0) selected.splice(idx, 1); else selected.push(value);
                answers[question.key] = selected.slice();
            };
            for (const opt of questionOptions(question)) {
                const tile = document.createElement('button');
                tile.type = 'button';
                tile.className = 'option-tile';
                tile.dataset.value = opt.value;
                tile.textContent = opt.label;
                if (selected.includes(opt.value)) tile.classList.add('selected');
                tile.addEventListener('click', () => {
                    toggle(opt.value);
                    tile.classList.toggle('selected');
                });
                wrap.appendChild(tile);
            }
            return wrap;
        }

        function renderNpsControl(question, currentValue) {
            const wrap = document.createElement('div');
            wrap.className = 'nps-scale';
            const update = () => {
                wrap.querySelectorAll('button').forEach(btn => {
                    btn.classList.toggle('selected', btn.dataset.value === String(answers[question.key] ?? ''));
                });
            };
            for (let i = 0; i <= 10; i++) {
                const btn = document.createElement('button');
                btn.type = 'button';
                btn.dataset.value = String(i);
                btn.textContent = String(i);
                btn.addEventListener('click', () => {
                    answers[question.key] = String(i);
                    update();
                });
                if (String(currentValue) === String(i)) btn.classList.add('selected');
                wrap.appendChild(btn);
            }
            update();
            return wrap;
        }

        function renderRatingControl(question, currentValue, stars) {
            const wrap = document.createElement('div');
            wrap.className = 'rating-stars';
            const update = (value) => {
                wrap.querySelectorAll('button').forEach(btn => {
                    const v = Number(btn.dataset.value);
                    btn.classList.toggle('selected', v <= value);
                });
            };
            const current = Number(currentValue ?? 0);
            for (let i = 1; i <= stars; i++) {
                const btn = document.createElement('button');
                btn.type = 'button';
                btn.dataset.value = String(i);
                btn.textContent = '★';
                btn.addEventListener('click', () => {
                    answers[question.key] = String(i);
                    update(i);
                });
                if (i <= current) btn.classList.add('selected');
                wrap.appendChild(btn);
            }
            if (current > 0) update(current);
            return wrap;
        }

        function validateSection(section) {
            const items = sectionQuestions(section.id).filter(questionVisible);
            for (const q of items) {
                if (!q.required) continue;
                const value = answers[q.key];
                if (Array.isArray(value) ? value.length === 0 : value == null || String(value).trim() === '') {
                    showMessage(`Please complete "${q.prompt}" before continuing.`, 'error');
                    return false;
                }
            }
            return true;
        }

        function nextSectionIndex(currentIdx) {
            const current = sections[currentIdx];
            if (!current) return currentIdx + 1;
            const currentQuestionIds = new Set(sectionQuestions(current.id).map(q => q.id));
            for (const rule of rules) {
                if (!currentQuestionIds.has(rule.sourceQuestionId)) continue;
                if (!rule.condition || !rule.action) continue;
                if (!evaluateCondition(rule.condition)) continue;
                const match = /skipTo\(section:'([^']+)'\)/i.exec(rule.action);
                if (match) {
                    const targetIndex = sections.findIndex(s => s.title === match[1]);
                    if (targetIndex >= 0) {
                        return targetIndex;
                    }
                }
            }
            return currentIdx + 1;
        }

        function collectPayload() {
            const payload = Object.create(null);
            for (const q of questions) {
                const type = (q.type || '').trim().toLowerCase();
                if (type === 'message' || type === 'static_text' || type === 'static-html' || type === 'static_html' || type === 'image' || type === 'video' || type === 'divider' || type === 'spacer') {
                    continue;
                }
                const raw = answers[q.key];
                if (Array.isArray(raw)) {
                    payload[q.key] = raw.length ? raw.join(',') : null;
                } else if (raw == null || String(raw).trim() === '') {
                    payload[q.key] = null;
                } else {
                    payload[q.key] = String(raw);
                }
            }
            payload.__startedUtc = new Date(sessionStarted).toISOString();
            payload.__durationSeconds = String(Math.max(0, Math.round((Date.now() - sessionStarted) / 1000)));
            return payload;
        }

        prevBtn.addEventListener('click', () => {
            if (pageIndex > 0) {
                pageIndex = Math.max(0, pageIndex - 1);
                render();
            }
        });

        nextBtn.addEventListener('click', () => {
            const section = sections[pageIndex];
            if (!section) return;
            if (!validateSection(section)) return;
            const nextIndex = Math.min(sections.length - 1, nextSectionIndex(pageIndex));
            pageIndex = nextIndex;
            render();
        });

        submitBtn.addEventListener('click', async () => {
            const section = sections[pageIndex];
            if (!section) return;
            if (!validateSection(section)) return;

            submitBtn.disabled = true;
            nextBtn.disabled = true;
            prevBtn.disabled = true;
            clearMessage();
            try {
                const payload = collectPayload();
                await API.submit(code, payload);
                showMessage('Thanks for your feedback!', 'success');
                setTimeout(() => appRedirect('thank-you.html'), 600);
            } catch (err) {
                console.error(err);
                showMessage('Unable to submit the survey right now. Please try again.', 'error');
                submitBtn.disabled = false;
                nextBtn.disabled = false;
                prevBtn.disabled = pageIndex === 0;
            }
        });

        async function load() {
            try {
                const [surveyMeta, def] = await Promise.all([
                    API.getSurvey(code).catch(() => null),
                    API.getSurveyDefinition(code)
                ]);

                definition = def;
                if (!definition || !Array.isArray(definition.questions)) {
                    throw new Error('Survey definition not found.');
                }

                sections = Array.isArray(definition.sections)
                    ? definition.sections.slice().sort((a, b) => (a.order ?? 0) - (b.order ?? 0))
                    : [];
                questions = definition.questions.slice().sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
                rules = Array.isArray(definition.rules) ? definition.rules.slice() : [];

                if (!sections.length) {
                    sections = [{ id: null, title: definition.title || 'Survey', order: 0, columns: 1 }];
                }

                // Remove empty sections to avoid blank pages
                sections = sections.filter(sec => sectionQuestions(sec.id).length > 0);
                if (!sections.length) {
                    sections = [{ id: null, title: definition.title || 'Survey', order: 0, columns: 1 }];
                }

                ensureDefaults();

                titleEl.textContent = definition.title || surveyMeta?.title || 'Survey';
                const description = definition.description || surveyMeta?.description;
                if (description) {
                    descEl.textContent = description;
                    descEl.classList.remove('hidden');
                } else {
                    descEl.classList.add('hidden');
                }

                if (definition.themeAccent) {
                    document.documentElement.style.setProperty('--accent', definition.themeAccent);
                }
                if (definition.themePanel) {
                    document.documentElement.style.setProperty('--panel', definition.themePanel);
                }
                if (definition.customCss) {
                    applyCustomCss(definition.customCss);
                }

                sessionStarted = Date.now();
                render();
            } catch (err) {
                console.error(err);
                showMessage('Unable to load the survey right now.', 'error');
                disableControls();
            }
        }

        load();
    })();
    
