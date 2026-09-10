import { cva, type VariantProps } from "class-variance-authority";
import { Slot } from "@radix-ui/react-slot";
import type { ButtonHTMLAttributes } from "react";
import { cn } from "@/lib/cn";

/**
 * 버튼 계층(2026-09-10 조사 합성): primary=액센트 채움(화면당 하나) · secondary=표면+헤어라인 · ghost=투명
 * · adopt/reject=연한 의미색 배경+진한 글자(채택 확정 단계에서만 primary 채움으로 승격). pill 금지, 높이 32–36.
 */
const buttonVariants = cva(
  "inline-flex items-center justify-center gap-1.5 whitespace-nowrap rounded-md font-medium transition-[background-color,border-color,opacity,transform] duration-[var(--motion-quick)] ease-[var(--ease-out)] focus-visible:outline-none focus-visible:ring-[3px] focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background disabled:pointer-events-none disabled:opacity-40 active:scale-[0.985]",
  {
    variants: {
      variant: {
        default: "bg-primary text-primary-foreground hover:opacity-90",
        secondary: "border border-border-strong bg-raised text-foreground hover:bg-elevated",
        ghost: "text-muted hover:bg-elevated hover:text-foreground",
        adopt: "border border-adopt-line/40 bg-adopt text-adopt-fg hover:border-adopt-line",
        reject: "border border-reject-line/40 bg-reject text-reject-fg hover:border-reject-line",
      },
      size: {
        default: "h-9 px-3.5 text-sm",
        sm: "h-8 px-3 text-[13px]",
        lg: "h-10 px-4 text-sm",
        touch: "h-9 min-w-9 px-3 text-sm",
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
