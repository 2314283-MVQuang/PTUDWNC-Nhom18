"use client";

import * as DialogPrimitive from "@radix-ui/react-dialog";
import { X } from "lucide-react";
import type { ReactNode } from "react";
import { Button } from "./Button";

/** Radix đảm nhiệm focus trap, Escape, phục hồi focus và ngữ nghĩa dialog. */
export function Dialog({ open, onOpenChange, trigger, title, description, children }: {
  open: boolean; onOpenChange: (open: boolean) => void;
  trigger: ReactNode; title: string; description: string; children: ReactNode;
}) {
  return <DialogPrimitive.Root open={open} onOpenChange={onOpenChange}>
    <DialogPrimitive.Trigger asChild>{trigger}</DialogPrimitive.Trigger>
    <DialogPrimitive.Portal>
      <DialogPrimitive.Overlay className="fixed inset-0 z-50 bg-neutral-950/50 backdrop-blur-sm" />
      <DialogPrimitive.Content className="fixed inset-x-3 top-1/2 z-50 mx-auto max-h-[90dvh] max-w-lg -translate-y-1/2 overflow-y-auto rounded-2xl bg-white p-6 shadow-xl focus:outline-none">
        <DialogPrimitive.Title className="pr-12 font-display text-xl font-bold">{title}</DialogPrimitive.Title>
        <DialogPrimitive.Description className="mt-2 pr-6 text-sm text-neutral-600">{description}</DialogPrimitive.Description>
        <DialogPrimitive.Close asChild><Button variant="ghost" size="sm" className="absolute right-3 top-3" aria-label="Đóng hộp thoại"><X aria-hidden="true" className="h-5 w-5" /></Button></DialogPrimitive.Close>
        <div className="mt-5">{children}</div>
      </DialogPrimitive.Content>
    </DialogPrimitive.Portal>
  </DialogPrimitive.Root>;
}
