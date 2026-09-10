import { cva, type VariantProps } from "class-variance-authority";
import { Slot } from "@radix-ui/react-slot";
import type { ButtonHTMLAttributes } from "react";
import { cn } from "@/lib/cn";

/**
 * 놀이터 단추(2026-09-10 「다섯 살도 쓸 만큼」): 알약 모양, 둥근 글씨(Do Hyeon), 아래 그림자가 있다가 누르면 내려앉는다.
 * default=하늘색(시키기) · secondary=흰 스티커 · ghost=투명 · adopt=민트(좋아, 해!) · reject=산호(아니야).
 */
const buttonVariants = cva(
  "btn-push inline-flex items-center justify-center gap-1.5 whitespace-nowrap rounded-full border-2 font-display transition-[background-color,border-color,opacity,transform,box-shadow] duration-[var(--motion-quick)] ease-[var(--ease-out)] focus-visible:outline-none focus-visible:ring-[3px] focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background disabled:pointer-events-none disabled:opacity-40",
  {
    variants: {
      variant: {
        default: "border-sky bg-sky text-primary-foreground [--btn-shadow:#1f7ad1] hover:brightness-105",
        secondary: "border-border bg-raised text-foreground hover:border-border-strong",
        ghost: "border-transparent text-muted shadow-none hover:bg-elevated hover:text-foreground",
        adopt: "border-mint bg-mint text-on-bright [--btn-shadow:#1fa66b] hover:brightness-105",
        reject: "border-coral-soft bg-coral-soft text-reject-fg [--btn-shadow:#f2b4b4] hover:border-coral",
      },
      size: {
        default: "h-10 px-4 text-[15px]",
        xs: "h-8 px-3 text-[13px]",
        sm: "h-9 px-3.5 text-sm",
        lg: "h-12 px-6 text-lg",
        touch: "h-11 min-w-11 px-4 text-base",
      },
    },
    defaultVariants: { variant: "default", size: "default" },
  },
);

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> &
  VariantProps<typeof buttonVariants> & { asChild?: boolean };

export function Button({ className, variant, size, asChild, ...props }: ButtonProps) {
  const Comp = asChild ? Slot : "button";
  return <Comp className={cn(buttonVariants({ variant, size }), className)} {...props} />;
}
