import { useEffect, useMemo, useState } from 'react'

const standaloneMediaQuery = '(display-mode: standalone)'

type NavigatorWithStandalone = Navigator & { standalone?: boolean }

export function isIosDevice(currentNavigator: Navigator = navigator) {
  return /iPhone|iPad|iPod/i.test(currentNavigator.userAgent)
    || (currentNavigator.platform === 'MacIntel' && currentNavigator.maxTouchPoints > 1)
}

export function isIpadDevice(currentNavigator: Navigator = navigator) {
  return /iPad/i.test(currentNavigator.userAgent)
    || (currentNavigator.platform === 'MacIntel' && currentNavigator.maxTouchPoints > 1)
}

export function isAndroidDevice(currentNavigator: Navigator = navigator) {
  return /Android/i.test(currentNavigator.userAgent)
}

export function isIosInstallableBrowser(currentNavigator: Navigator = navigator) {
  if (!isIosDevice(currentNavigator)) return false
  const userAgent = currentNavigator.userAgent
  const isBrowser = /Safari|CriOS|FxiOS|EdgiOS|OPiOS/i.test(userAgent)
  const isEmbeddedWebView = /FBAN|FBAV|Instagram|\bLine\b/i.test(userAgent)
  return isBrowser && !isEmbeddedWebView
}

export function readPwaDisplayMode(
  currentWindow: Window = window,
  currentNavigator: Navigator = navigator,
) {
  const iosStandalone = (currentNavigator as NavigatorWithStandalone).standalone === true
  const standalone = currentWindow.matchMedia(standaloneMediaQuery).matches || iosStandalone
  return {
    isStandalone: standalone,
    isIos: isIosDevice(currentNavigator),
    isIpad: isIpadDevice(currentNavigator),
    isAndroid: isAndroidDevice(currentNavigator),
    isIosInstallable: isIosInstallableBrowser(currentNavigator),
    isIosStandalone: iosStandalone,
  }
}

export function usePwaDisplayMode() {
  const [mode, setMode] = useState(readPwaDisplayMode)

  useEffect(() => {
    const query = window.matchMedia(standaloneMediaQuery)
    const update = () => setMode(readPwaDisplayMode())
    if (query.addEventListener) query.addEventListener('change', update)
    else query.addListener(update)
    return () => {
      if (query.removeEventListener) query.removeEventListener('change', update)
      else query.removeListener(update)
    }
  }, [])

  return useMemo(() => mode, [mode])
}
