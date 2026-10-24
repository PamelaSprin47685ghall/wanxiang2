import * as RDialog from "@radix-ui/react-dialog";
import { X } from "lucide-react";
import type { ReactNode } from "react";

export function Dialog({
  open,
  title,
  description,
  onClose,
  children,
  narrow,
  variant = "settings",
}: {
  open: boolean;
  title: string;
  description: string;
  onClose: () => void;
  children: ReactNode;
  narrow?: boolean;
  variant?: "settings" | "confirm" | "palette";
}) {
  const kind = narrow ? "confirm" : variant;
  return (
    <RDialog.Root open={open} onOpenChange={(next) => !next && onClose()}>
      <RDialog.Portal>
        <RDialog.Overlay className="overlay" />
        <RDialog.Content className={`dialog dialog-${kind}`}>
          <div className="dialog-head">
            <RDialog.Title className="dialog-title">{title}</RDialog.Title>
            <RDialog.Close className="icon-btn" aria-label="关闭">
              <X className="icon" aria-hidden />
            </RDialog.Close>
          </div>
          <RDialog.Description className="sr-only">{description}</RDialog.Description>
          {children}
        </RDialog.Content>
      </RDialog.Portal>
    </RDialog.Root>
  );
}
