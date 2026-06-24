window.ChronoTrialsGame = window.ChronoTrialsGame || (function () {
    let unityInstance = null;
    let loadingPromise = null;

    function unityShowBanner(msg, type) {
        const warningBanner = document.querySelector('#unity-warning');
        if (!warningBanner) {
            return;
        }

        function updateBannerVisibility() {
            warningBanner.style.display = warningBanner.children.length ? 'block' : 'none';
        }

        const div = document.createElement('div');
        div.innerHTML = msg;
        warningBanner.appendChild(div);
        if (type === 'error') {
            div.style = 'background: red; padding: 10px;';
        } else {
            if (type === 'warning') {
                div.style = 'background: yellow; padding: 10px;';
            }

            setTimeout(function () {
                warningBanner.removeChild(div);
                updateBannerVisibility();
            }, 5000);
        }

        updateBannerVisibility();
    }

    function loadUnityScript() {
        return new Promise(function (resolve, reject) {
            if (window.createUnityInstance) {
                resolve();
                return;
            }

            const existingScript = document.querySelector('script[data-unity-loader="true"]');
            if (existingScript) {
                existingScript.addEventListener('load', function () {
                    resolve();
                }, { once: true });
                existingScript.addEventListener('error', function () {
                    reject(new Error('Unity loader kon niet worden geladen.'));
                }, { once: true });
                return;
            }

            const script = document.createElement('script');
            script.dataset.unityLoader = 'true';
            script.src = '/Build/WebGL.loader.js';
            script.onload = function () {
                resolve();
            };
            script.onerror = function () {
                reject(new Error('Unity loader kon niet worden geladen.'));
            };
            document.body.appendChild(script);
        });
    }

    function setLoadingError(message) {
        const loading = document.getElementById('unity-loading');
        if (!loading) {
            return;
        }

        loading.innerHTML =
            '<p style="font-family:Orbitron,monospace;color:#f04a4a;letter-spacing:0.2em;">' + message + '</p>' +
            '<p style="color:var(--text-dim);font-size:12px;margin-top:0.5rem;">Plaats de Unity WebGL-build in de Build-map van het project.</p>';
    }

    async function start() {
        if (unityInstance || loadingPromise) {
            return loadingPromise;
        }

        const canvas = document.querySelector('#unity-canvas');
        const loading = document.getElementById('unity-loading');
        const progressBar = document.getElementById('unity-progress');
        const progressText = document.getElementById('unity-progress-text');

        if (!canvas || !loading) {
            return;
        }

        loadingPromise = (async function () {
            try {
                await loadUnityScript();

                const buildUrl = '/Build';
                const config = {
                    arguments: [],
                    dataUrl: buildUrl + '/WebGL.data',
                    frameworkUrl: buildUrl + '/WebGL.framework.js',
                    codeUrl: buildUrl + '/WebGL.wasm',
                    streamingAssetsUrl: '/StreamingAssets',
                    companyName: 'DefaultCompany',
                    productName: 'Amura’s water world',
                    productVersion: '1.0',
                    showBanner: unityShowBanner
                };

                if (/iPhone|iPad|iPod|Android/i.test(navigator.userAgent)) {
                    const meta = document.createElement('meta');
                    meta.name = 'viewport';
                    meta.content = 'width=device-width, height=device-height, initial-scale=1.0, user-scalable=no, shrink-to-fit=yes';
                    document.getElementsByTagName('head')[0].appendChild(meta);

                    const container = document.querySelector('#unity-container');
                    if (container) {
                        container.className = 'unity-mobile';
                    }
                    canvas.className = 'unity-mobile';
                } else {
                    canvas.style.width = '960px';
                    canvas.style.height = '600px';
                }

                loading.style.display = 'block';

                unityInstance = await createUnityInstance(canvas, config, function (progress) {
                    const pct = Math.round(progress * 100);
                    if (progressBar) {
                        progressBar.style.width = pct + '%';
                    }
                    if (progressText) {
                        progressText.textContent = pct + '%';
                    }
                });

                loading.style.display = 'none';
                const fullscreenButton = document.querySelector('#unity-fullscreen-button');
                if (fullscreenButton) {
                    fullscreenButton.onclick = function () {
                        unityInstance.SetFullscreen(1);
                    };
                }
            } catch (message) {
                console.error(message);
                setLoadingError('BUILD KON NIET WORDEN GELADEN');
            }
        })();

        return loadingPromise;
    }

    function fullscreen() {
        if (unityInstance) {
            unityInstance.SetFullscreen(1);
        }
    }

    return {
        start: start,
        fullscreen: fullscreen
    };
})();