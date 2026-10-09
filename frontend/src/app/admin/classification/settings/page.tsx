import { SettingsForm, type Settings } from "@/components/admin/settings-form";
import { EmptyState } from "@/components/ui";
import { adminGet, canManageSettings, requireStaff } from "@/lib/admin";
import { saveClassificationSettingsAction } from "@/lib/admin-actions";

export default async function ClassificationSettingsPage() {
  const staff = await requireStaff();
  if (!staff || !canManageSettings(staff.permissions)) {
    return <EmptyState>Seuls les administrateurs peuvent modifier ces paramètres.</EmptyState>;
  }
  const settings = await adminGet<Settings>("/api/admin/classification/settings");

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-semibold">Paramètres de contribution</h1>
        <p className="mt-1 max-w-prose text-sm text-ink-muted">
          Tout est configurable ici : taille des tâches, règles de consensus, déclencheurs et récompenses.
          Chaque modification est journalisée.
        </p>
      </header>
      <SettingsForm settings={settings} action={saveClassificationSettingsAction} />
    </div>
  );
}
