use serde::{Deserialize, Serialize};
use std::path::Path;
use tokio::process::Command;

#[derive(Debug, Clone, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct TrustMetadata {
    pub signed: bool,
    pub publisher: Option<String>,
}

pub fn assess_risk(
    path: Option<&str>,
    publisher: Option<&str>,
    signed: bool,
    process_name: Option<&str>,
) -> String {
    let Some(path) = path else {
        return "unknown".to_string();
    };
    let path_lower = path.to_lowercase();
    let name_lower = process_name.unwrap_or(path).to_lowercase();

    let suspicious_patterns = ["svch0st", "exp1orer", "1sass", "csrsss"];
    if suspicious_patterns.iter().any(|p| name_lower.contains(p)) {
        return "suspicious".to_string();
    }

    let suspicious_paths = ["\\temp\\", "\\tmp\\", "\\appdata\\local\\temp\\", "/tmp/"];
    if !signed && suspicious_paths.iter().any(|p| path_lower.contains(p)) {
        return "high".to_string();
    }

    if !signed {
        return "medium".to_string();
    }

    if publisher.is_some() {
        return "low".to_string();
    }

    "low".to_string()
}

pub fn quick_trust_from_path(path: Option<&str>) -> TrustMetadata {
    let Some(path) = path else {
        return TrustMetadata::default();
    };
    let lower = path.to_lowercase();

    // Fast path heuristic for list views; full validation is used for details.
    let likely_signed = lower.contains("\\windows\\")
        || lower.contains("\\program files\\")
        || lower.contains("/usr/bin/")
        || lower.contains("/bin/");

    TrustMetadata {
        signed: likely_signed,
        publisher: None,
    }
}

pub async fn get_signature_details(path: &str) -> TrustMetadata {
    if path.is_empty() || !Path::new(path).exists() {
        return TrustMetadata::default();
    }

    #[cfg(target_os = "windows")]
    {
        return get_signature_windows(path).await;
    }

    #[cfg(not(target_os = "windows"))]
    {
        quick_trust_from_path(Some(path))
    }
}

#[cfg(target_os = "windows")]
async fn get_signature_windows(path: &str) -> TrustMetadata {
    let escaped = path.replace('\'', "''");
    let script = format!(
        "(Get-AuthenticodeSignature -FilePath '{escaped}' | Select-Object Status,SignerCertificate | ConvertTo-Json -Depth 4)"
    );

    let output = Command::new("powershell")
        .arg("-NoProfile")
        .arg("-Command")
        .arg(script)
        .output()
        .await;

    let Ok(output) = output else {
        return quick_trust_from_path(Some(path));
    };
    if !output.status.success() {
        return quick_trust_from_path(Some(path));
    }

    let value: serde_json::Value = serde_json::from_slice(&output.stdout).unwrap_or_default();
    let status = value
        .get("Status")
        .and_then(serde_json::Value::as_str)
        .unwrap_or_default()
        .to_string();

    let signed = status.eq_ignore_ascii_case("Valid");
    let publisher = value
        .get("SignerCertificate")
        .and_then(|x| x.get("Subject"))
        .and_then(serde_json::Value::as_str)
        .and_then(subject_cn);

    TrustMetadata { signed, publisher }
}

fn subject_cn(subject: &str) -> Option<String> {
    let token = "CN=";
    let idx = subject.find(token)?;
    let rest = &subject[idx + token.len()..];
    let end = rest.find(',').unwrap_or(rest.len());
    Some(rest[..end].trim().to_string())
}
