import React from "react";
import { QRCodeSVG } from "qrcode.react";
import { getValidationErrors, sendRequest } from "../../hooks/apiClient";
import { useStatefulApi } from "../../hooks/useStatefulApi";
import {
  accountUrls,
  type EnableTwoFactorRequest,
  type RecoveryCodesResponse,
  type TwoFactorSetupResponse,
} from "../../api/account";
import "../account/AccountPages.css";

type Props = {
  /** Called after the user has saved the recovery codes. */
  onCompleted: () => void;
};

/** Setting up an authenticator app (required for administrators). */
const TwoFactorSetup: React.FC<Props> = ({ onCompleted }) => {
  const setup = useStatefulApi<TwoFactorSetupResponse>({ serviceUrl: accountUrls.twoFactor });
  const loadSetup = setup.get;

  const [code, setCode] = React.useState("");
  const [errors, setErrors] = React.useState<string[]>([]);
  const [recoveryCodes, setRecoveryCodes] = React.useState<string[] | null>(null);
  const [isSubmitting, setIsSubmitting] = React.useState(false);

  React.useEffect(() => {
    void loadSetup();
  }, [loadSetup]);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setIsSubmitting(true);
    setErrors([]);
    try {
      const response = await sendRequest<RecoveryCodesResponse, EnableTwoFactorRequest>("POST", {
        serviceUrl: accountUrls.enableTwoFactor,
        body: { code },
      });
      setRecoveryCodes(response?.recoveryCodes ?? []);
    } catch (caught) {
      const fieldErrors = getValidationErrors(caught);
      setErrors(fieldErrors.code ?? ["Die Einrichtung ist fehlgeschlagen. Bitte versuche es erneut."]);
    } finally {
      setIsSubmitting(false);
    }
  };

  if (recoveryCodes) {
    return (
      <section>
        <h2>Wiederherstellungscodes</h2>
        <p>
          Die Zwei-Faktor-Anmeldung ist aktiv. Bewahre diese Codes sicher auf: Mit jedem Code kannst du dich
          einmal anmelden, falls du keinen Zugriff auf die Authenticator-App hast. Sie werden nur jetzt angezeigt.
        </p>
        <ul className="account-codes">
          {recoveryCodes.map((recoveryCode) => (
            <li key={recoveryCode}>
              <code>{recoveryCode}</code>
            </li>
          ))}
        </ul>
        <div className="account-form">
          <button type="button" onClick={onCompleted}>
            Ich habe die Codes gespeichert
          </button>
        </div>
      </section>
    );
  }

  return (
    <section>
      <h2>Zwei-Faktor-Anmeldung einrichten</h2>
      <p>Für den Admin-Bereich ist eine Authenticator-App (z. B. Microsoft oder Google Authenticator) erforderlich.</p>
      {setup.data && (
        <ol className="account-steps">
          <li>
            Scanne den QR-Code mit der App:
            <div className="account-qr">
              <QRCodeSVG value={setup.data.authenticatorUri} size={192} marginSize={2} title="QR-Code für die Authenticator-App" />
            </div>
            Falls das Scannen nicht klappt, gib diesen Schlüssel von Hand ein:
            <br />
            <code className="account-key">{setup.data.sharedKey.match(/.{1,4}/g)?.join(" ")}</code>
          </li>
          <li>Gib den sechsstelligen Code ein, den die App anzeigt.</li>
        </ol>
      )}
      <form className="account-form" onSubmit={submit} noValidate>
        <div className="account-field">
          <label htmlFor="setup-code">Code aus der App</label>
          <input
            id="setup-code"
            type="text"
            inputMode="numeric"
            autoComplete="one-time-code"
            value={code}
            onChange={(event) => setCode(event.target.value)}
            required
          />
        </div>
        {errors.map((message) => (
          <p key={message} className="account-error" role="alert">
            {message}
          </p>
        ))}
        <button type="submit" disabled={isSubmitting || !setup.data}>
          {isSubmitting ? "Wird geprüft …" : "Aktivieren"}
        </button>
      </form>
    </section>
  );
};

export default TwoFactorSetup;
