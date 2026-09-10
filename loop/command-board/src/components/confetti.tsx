import { useEffect, useRef } from "react";
import { useBoardStore } from "@/lib/board-store";

/**
 * 도장 「좋아, 해!」가 성공하면 1.2초 색종이(Duolingo·Trophy식 보상 피드백, 소리 없음).
 * 움직임 줄이기 설정이면 안 그린다. 캔버스 하나, 다섯 색.
 */
const COLORS = ["#3aa0ff", "#2fd08a", "#ff6b6b", "#ffd84d", "#9b7bff"];

export function Confetti() {
  const celebrate = useBoardStore((s) => s.celebrate);
  const ref = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    if (!celebrate) return;
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
    const canvas = ref.current;
    const ctx = canvas?.getContext("2d");
    if (!canvas || !ctx) return;
    const dpr = window.devicePixelRatio || 1;
    canvas.width = window.innerWidth * dpr;
    canvas.height = window.innerHeight * dpr;
    ctx.scale(dpr, dpr);
    const W = window.innerWidth;
    const bits = Array.from({ length: 90 }, () => ({
      x: W * (0.3 + Math.random() * 0.4),
      y: -10,
      vx: (Math.random() - 0.5) * 9,
      vy: 2 + Math.random() * 6,
      r: 4 + Math.random() * 5,
      a: Math.random() * Math.PI,
      va: (Math.random() - 0.5) * 0.3,
      c: COLORS[Math.floor(Math.random() * COLORS.length)],
    }));
    const t0 = performance.now();
    let raf = 0;
    const tick = (t: number) => {
      const k = (t - t0) / 1200;
      ctx.clearRect(0, 0, W, window.innerHeight);
      if (k >= 1) return;
      ctx.globalAlpha = 1 - k * k;
      for (const b of bits) {
        b.x += b.vx;
        b.y += b.vy;
        b.vy += 0.18;
        b.a += b.va;
        ctx.save();
        ctx.translate(b.x, b.y);
        ctx.rotate(b.a);
        ctx.fillStyle = b.c;
        ctx.fillRect(-b.r / 2, -b.r / 3, b.r, b.r / 1.5);
        ctx.restore();
      }
      raf = requestAnimationFrame(tick);
    };
    raf = requestAnimationFrame(tick);
    return () => {
      cancelAnimationFrame(raf);
      ctx.clearRect(0, 0, W, window.innerHeight);
    };
  }, [celebrate]);

  return <canvas ref={ref} aria-hidden className="pointer-events-none fixed inset-0 z-40 h-full w-full" />;
}
