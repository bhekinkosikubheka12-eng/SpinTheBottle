/**
 * Spin the Bottle - Hardware-Accelerated Physics & Animation Engine
 * Features:
 * - Cumulative forward-only rotation (never snaps back to 0)
 * - Cubic-bezier inertial deceleration with natural settling bounce: cubic-bezier(0.15, 0.85, 0.35, 1.02)
 * - GPU acceleration (will-change: transform)
 * - Web Audio API procedural ratchet clicks and win chimes (zero external audio files needed)
 * - Haptic feedback (navigator.vibrate)
 * - Canvas touch gesture prevention (no pull-to-refresh or pinch-zoom)
 */

window.BottlePhysics = (function () {
    let cumulativeAngle = 0;
    let isSpinning = false;
    let dotNetHelper = null;
    let bottleElement = null;
    let audioCtx = null;
    let soundEnabled = true;
    let clickInterval = null;

    function initAudio() {
        if (!audioCtx) {
            const AudioContext = window.AudioContext || window.webkitAudioContext;
            if (AudioContext) {
                audioCtx = new AudioContext();
            }
        }
        if (audioCtx && audioCtx.state === 'suspended') {
            audioCtx.resume();
        }
    }

    function playTickSound(frequency, volume) {
        if (!soundEnabled || !audioCtx) return;
        try {
            const osc = audioCtx.createOscillator();
            const gain = audioCtx.createGain();

            osc.type = 'triangle';
            osc.frequency.setValueAtTime(frequency || 540, audioCtx.currentTime);
            osc.frequency.exponentialRampToValueAtTime(120, audioCtx.currentTime + 0.04);

            gain.gain.setValueAtTime(volume || 0.15, audioCtx.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.001, audioCtx.currentTime + 0.04);

            osc.connect(gain);
            gain.connect(audioCtx.destination);

            osc.start();
            osc.stop(audioCtx.currentTime + 0.04);
        } catch (e) {
            // Audio context failed or muted
        }
    }

    function playWinSound() {
        if (!soundEnabled || !audioCtx) return;
        try {
            const now = audioCtx.currentTime;
            const notes = [523.25, 659.25, 783.99, 1046.50]; // C5, E5, G5, C6
            notes.forEach((freq, idx) => {
                const osc = audioCtx.createOscillator();
                const gain = audioCtx.createGain();

                osc.type = 'sine';
                osc.frequency.setValueAtTime(freq, now + idx * 0.09);

                gain.gain.setValueAtTime(0.2, now + idx * 0.09);
                gain.gain.exponentialRampToValueAtTime(0.001, now + idx * 0.09 + 0.35);

                osc.connect(gain);
                gain.connect(audioCtx.destination);

                osc.start(now + idx * 0.09);
                osc.stop(now + idx * 0.09 + 0.35);
            });
        } catch (e) { }
    }

    function playLossSound() {
        if (!soundEnabled || !audioCtx) return;
        try {
            const now = audioCtx.currentTime;
            const osc = audioCtx.createOscillator();
            const gain = audioCtx.createGain();

            osc.type = 'sawtooth';
            osc.frequency.setValueAtTime(180, now);
            osc.frequency.exponentialRampToValueAtTime(70, now + 0.25);

            gain.gain.setValueAtTime(0.18, now);
            gain.gain.exponentialRampToValueAtTime(0.001, now + 0.25);

            osc.connect(gain);
            gain.connect(audioCtx.destination);

            osc.start(now);
            osc.stop(now + 0.25);
        } catch (e) { }
    }

    function triggerHaptic(pattern) {
        if ('vibrate' in navigator) {
            try {
                navigator.vibrate(pattern || [15, 50, 15]);
            } catch (e) { }
        }
    }

    function simulateDecelerationClicks(durationSeconds) {
        if (!soundEnabled) return;
        initAudio();

        const startTime = Date.now();
        const totalDuration = durationSeconds * 1000;
        let lastClickTime = startTime;
        let delay = 35; // start fast (milliseconds between clicks)

        function scheduleNextClick() {
            if (!isSpinning) return;
            const elapsed = Date.now() - startTime;
            const progress = elapsed / totalDuration;

            if (progress >= 0.98) return;

            // Deceleration curve for ratchet sound
            // As progress increases, delay stretches from 35ms to 350ms
            delay = 35 + Math.pow(progress, 2.5) * 320;
            playTickSound(650 - progress * 200, Math.max(0.04, 0.18 * (1 - progress * 0.6)));

            clickInterval = setTimeout(scheduleNextClick, delay);
        }

        scheduleNextClick();
    }

    return {
        init: function (elementId, dotNetReference) {
            dotNetHelper = dotNetReference;
            bottleElement = document.getElementById(elementId);

            // Prevent mobile pull-to-refresh and pinch-zoom on the game canvas/board
            const boardContainer = document.querySelector('.game-canvas-area');
            if (boardContainer) {
                boardContainer.addEventListener('touchmove', function (e) {
                    if (e.touches.length > 1) {
                        e.preventDefault();
                    }
                }, { passive: false });

                boardContainer.addEventListener('gesturestart', function (e) {
                    e.preventDefault();
                });
            }

            // Global listener on first interaction to unlock AudioContext
            const unlockAudio = function () {
                initAudio();
                document.removeEventListener('click', unlockAudio);
                document.removeEventListener('touchstart', unlockAudio);
            };
            document.addEventListener('click', unlockAudio);
            document.addEventListener('touchstart', unlockAudio);

            console.log('BottlePhysics engine initialized.');
        },

        spinTo: function (targetAngle, durationSeconds, roundId, isWin) {
            if (isSpinning) return;
            if (!bottleElement) {
                bottleElement = document.getElementById('bottle-rotor');
            }
            if (!bottleElement) {
                console.error('Bottle rotor element not found.');
                return;
            }

            initAudio();
            isSpinning = true;
            if (clickInterval) clearTimeout(clickInterval);

            durationSeconds = durationSeconds || 4.0;

            // Cumulative forward momentum calculation:
            // Ensure bottle spins multiple full turns (5 to 7 turns = 1800 to 2520 deg)
            // and lands precisely on targetAngle without snapping back or rewinding.
            const minFullSpins = 5;
            const currentMod = ((cumulativeAngle % 360) + 360) % 360;
            let forwardDelta = (targetAngle - currentMod + 360) % 360;

            // If delta is tiny, add one more full spin for visual impact
            if (forwardDelta < 40) {
                forwardDelta += 360;
            }

            const targetCumulative = cumulativeAngle + (minFullSpins * 360) + forwardDelta;
            cumulativeAngle = targetCumulative;

            // Configure hardware-accelerated cubic-bezier transition
            bottleElement.style.willChange = 'transform';
            bottleElement.style.transition = `transform ${durationSeconds}s cubic-bezier(0.15, 0.85, 0.35, 1.02)`;
            bottleElement.style.transform = `rotate(${cumulativeAngle}deg)`;

            // Start simulated ratchet deceleration sound clicks
            simulateDecelerationClicks(durationSeconds);

            // Transition end handler
            const onTransitionEnd = function (e) {
                // Ensure event is from bottle transform
                if (e.target !== bottleElement || e.propertyName !== 'transform') return;

                bottleElement.removeEventListener('transitionend', onTransitionEnd);
                isSpinning = false;
                if (clickInterval) clearTimeout(clickInterval);

                // Natural settle haptic feedback
                triggerHaptic([15, 50, 15]);

                // Play win or loss sound
                if (isWin) {
                    playWinSound();
                    setTimeout(() => triggerHaptic([30, 60, 30, 60, 40]), 120);
                } else {
                    playLossSound();
                }

                // Callback to Blazor
                if (dotNetHelper) {
                    dotNetHelper.invokeMethodAsync('OnSpinAnimationCompleted', roundId);
                }
            };

            bottleElement.addEventListener('transitionend', onTransitionEnd);
        },

        getCurrentAngle: function () {
            return ((cumulativeAngle % 360) + 360) % 360;
        },

        toggleSound: function () {
            soundEnabled = !soundEnabled;
            return soundEnabled;
        },

        isSoundOn: function () {
            return soundEnabled;
        },

        vibrate: function (pattern) {
            triggerHaptic(pattern);
        },

        pickDeviceContacts: async function () {
            if ('contacts' in navigator && 'ContactsManager' in window && navigator.contacts && navigator.contacts.select) {
                try {
                    // Check supported properties according to W3C Contact Picker API specification
                    let props = ['name', 'tel'];
                    if (navigator.contacts.getProperties) {
                        const supported = await navigator.contacts.getProperties();
                        props = props.filter(p => supported.includes(p));
                    }
                    if (props.length === 0) return null;

                    const opts = { multiple: true };
                    // Prompts native OS contact picker permission & contact selection sheet
                    const contacts = await navigator.contacts.select(props, opts);
                    if (contacts && contacts.length > 0) {
                        return contacts.map((c, i) => {
                            const name = (c.name && c.name.length > 0) ? c.name[0] : `Contact ${i + 1}`;
                            const phone = (c.tel && c.tel.length > 0) ? c.tel[0] : '';
                            return {
                                id: 'native_' + i + '_' + Date.now(),
                                name: name,
                                phoneNumber: phone,
                                isSelected: true
                            };
                        }).filter(c => c.phoneNumber.length > 0 || c.name.length > 0);
                    }
                } catch (err) {
                    console.log('Native contact picker dismissed or permission denied:', err);
                }
            }
            return null;
        },

        shareExperience: async function (title, text, phoneNumbers) {
            triggerHaptic([20, 40, 20]);
            let sharedViaApi = false;

            if (navigator.share) {
                try {
                    await navigator.share({
                        title: title || 'Spin the Bottle Win!',
                        text: text,
                        url: window.location.href
                    });
                    sharedViaApi = true;
                } catch (e) {
                    console.log('Web share cancelled or unsupported.');
                }
            }

            if (!sharedViaApi && phoneNumbers && phoneNumbers.length > 0) {
                try {
                    const isApple = /iPhone|iPad|iPod/i.test(navigator.userAgent);
                    const separator = isApple ? '&' : '?';
                    const phones = phoneNumbers.join(';');
                    const smsLink = `sms:${encodeURIComponent(phones)}${separator}body=${encodeURIComponent(text)}`;
                    const a = document.createElement('a');
                    a.href = smsLink;
                    a.style.display = 'none';
                    document.body.appendChild(a);
                    a.click();
                    setTimeout(() => a.remove(), 1000);
                } catch (e) {
                    console.log('SMS intent trigger failed:', e);
                }
            }
            return true;
        },

        playCelebrationSound: function () {
            initAudio();
            playWinSound();
            setTimeout(() => triggerHaptic([30, 60, 30, 60, 40]), 100);
        }
    };
})();
