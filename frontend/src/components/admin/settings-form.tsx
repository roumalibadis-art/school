"use client";

import { useActionState } from "react";
import { Button, Card } from "@/components/ui";
import type { SettingsState } from "@/lib/admin-actions";

export interface Settings {
  documentsPerTask: number; assignmentExpiryHours: number; minSecondsBeforeVote: number; requiredVoters: number;
  agreementPercent: number; requiredFields: string[]; nonEducationalPercent: number; nonEducationalPolicy: string;
  loginTriggerEnabled: boolean; downloadTriggerEnabled: boolean; downloadsPerPrompt: number; promptSnoozeMinutes: number;
  quotaEnabled: boolean; freeDownloadsPerWindow: number; quotaWindowDays: number; bonusDownloadsPerContribution: number;
  maxBonusPerWindow: number; maxRewardedContributionsPerDay: number; maxPendingProposalsPerUser: number;
}

const FIELDS = ["Specialty", "Department", "DocumentType", "AcademicYear", "Session"];
const FIELD_LABELS: Record<string, string> = {
  Specialty: "Spécialité", Department: "Département", DocumentType: "Type de document",
  AcademicYear: "Année universitaire", Session: "Type d’examen / session",
};

function Num({ name, label, value, min, max, hint }: { name: string; label: string; value: number; min: number; max: number; hint?: string }) {
  return (
    <label className="block text-sm">
      <span className="mb-1 block font-medium text-ink-muted">{label}</span>
      <input type="number" name={name} defaultValue={value} min={min} max={max} required
        className="w-full rounded-md border border-line bg-paper-raised px-3 py-2" />
      {hint ? <span className="mt-1 block text-xs text-ink-soft">{hint}</span> : null}
    </label>
  );
}

function Check({ name, label, checked, hint }: { name: string; label: string; checked: boolean; hint?: string }) {
  return (
    <label className="flex items-start gap-2 text-sm">
      <input type="checkbox" name={name} defaultChecked={checked} className="mt-1" />
      <span><span className="font-medium">{label}</span>{hint ? <span className="block text-xs text-ink-soft">{hint}</span> : null}</span>
    </label>
  );
}

export function SettingsForm({ settings, action }: {
  settings: Settings;
  action: (prev: SettingsState, formData: FormData) => Promise<SettingsState>;
}) {
  const [state, formAction, pending] = useActionState(action, {});
  return (
    <form action={formAction} className="space-y-6">
      <Card className="space-y-4 p-4">
        <h2 className="font-semibold">Tâche de classification</h2>
        <div className="grid gap-4 sm:grid-cols-3">
          <Num name="documentsPerTask" label="Documents par tâche" value={settings.documentsPerTask} min={1} max={10} />
          <Num name="assignmentExpiryHours" label="Expiration (heures)" value={settings.assignmentExpiryHours} min={1} max={336} hint="Après ce délai, la place est libérée." />
          <Num name="minSecondsBeforeVote" label="Délai minimal avant réponse (s)" value={settings.minSecondsBeforeVote} min={0} max={120} hint="Anti-clics automatiques." />
        </div>
      </Card>

      <Card className="space-y-4 p-4">
        <h2 className="font-semibold">Consensus et vérification</h2>
        <div className="grid gap-4 sm:grid-cols-3">
          <Num name="requiredVoters" label="Votants distincts requis" value={settings.requiredVoters} min={1} max={15} />
          <Num name="agreementPercent" label="Accord requis (%)" value={settings.agreementPercent} min={51} max={100}
            hint="Part des votes qui doit s’accorder sur chaque champ obligatoire." />
          <Num name="nonEducationalPercent" label="Seuil « non pédagogique » (%)" value={settings.nonEducationalPercent} min={51} max={100} />
        </div>
        <fieldset>
          <legend className="mb-1 text-sm font-medium text-ink-muted">Champs obligatoires pour une vérification automatique</legend>
          <div className="flex flex-wrap gap-4">
            {FIELDS.map((f) => (
              <label key={f} className="flex items-center gap-2 text-sm">
                <input type="checkbox" name="requiredFields" value={f} defaultChecked={settings.requiredFields.includes(f)} />
                {FIELD_LABELS[f]}
              </label>
            ))}
          </div>
          <p className="mt-1 text-xs text-ink-soft">Les autres champs sont appliqués seulement si au moins deux votants s’accordent, sans bloquer la vérification.</p>
        </fieldset>
        <label className="block text-sm">
          <span className="mb-1 block font-medium text-ink-muted">Si la majorité juge le document non pédagogique</span>
          <select name="nonEducationalPolicy" defaultValue={settings.nonEducationalPolicy}
            className="w-full max-w-md rounded-md border border-line bg-paper-raised px-3 py-2">
            <option value="SendToReview">Envoyer à un administrateur (recommandé)</option>
            <option value="AutoReject">Rejeter automatiquement</option>
          </select>
        </label>
      </Card>

      <Card className="space-y-4 p-4">
        <h2 className="font-semibold">Déclencheurs</h2>
        <Check name="loginTriggerEnabled" label="À la connexion" checked={settings.loginTriggerEnabled} hint="Une invitation par connexion, jamais pendant un paiement." />
        <Check name="downloadTriggerEnabled" label="Après un nombre de téléchargements" checked={settings.downloadTriggerEnabled} />
        <div className="grid gap-4 sm:grid-cols-2">
          <Num name="downloadsPerPrompt" label="Téléchargements entre deux invitations" value={settings.downloadsPerPrompt} min={1} max={1000} />
          <Num name="promptSnoozeMinutes" label="Délai « plus tard » (minutes)" value={settings.promptSnoozeMinutes} min={1} max={10080} />
        </div>
      </Card>

      <Card className="space-y-4 p-4">
        <h2 className="font-semibold">Récompenses (téléchargements gratuits)</h2>
        <Check name="quotaEnabled" label="Limiter les téléchargements gratuits" checked={settings.quotaEnabled}
          hint="Désactivé : aucun changement pour les utilisateurs actuels. Premium et équipe ne sont jamais limités ; le contenu Premium reste réservé aux abonnés." />
        <div className="grid gap-4 sm:grid-cols-3">
          <Num name="freeDownloadsPerWindow" label="Téléchargements gratuits par période" value={settings.freeDownloadsPerWindow} min={0} max={100000} />
          <Num name="quotaWindowDays" label="Durée de la période (jours)" value={settings.quotaWindowDays} min={1} max={366} />
          <Num name="bonusDownloadsPerContribution" label="Bonus par contribution valide" value={settings.bonusDownloadsPerContribution} min={0} max={1000} />
          <Num name="maxBonusPerWindow" label="Bonus maximal par période" value={settings.maxBonusPerWindow} min={0} max={100000} />
          <Num name="maxRewardedContributionsPerDay" label="Contributions récompensées / jour" value={settings.maxRewardedContributionsPerDay} min={1} max={10000} hint="Plafond anti-abus." />
          <Num name="maxPendingProposalsPerUser" label="Propositions en attente / utilisateur" value={settings.maxPendingProposalsPerUser} min={1} max={1000} />
        </div>
        <p className="text-xs text-ink-soft">Passer un document ne coûte rien et ne rapporte rien. Les doublons et les clics répétés ne sont jamais récompensés.</p>
      </Card>

      {state.message ? (
        <p role={state.ok ? "status" : "alert"} className={`rounded-md p-3 text-sm ${state.ok ? "bg-brand-soft text-brand-strong" : "bg-red-50 text-red-800"}`}>
          {state.message}
        </p>
      ) : null}
      <Button type="submit" disabled={pending}>{pending ? "Enregistrement…" : "Enregistrer les paramètres"}</Button>
    </form>
  );
}
