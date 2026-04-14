$file = "PackageHandlerPanel.cs"
$content = Get-Content $file -Raw

# Find the method and replace it
$oldMethod = '(?s)private Panel CreateProjectUpdateCard\(ProjectInfo project\).*?(?=\n        private)'
$newMethod = Get-Content "updated_card_method.txt" -Raw

# Using regex to replace the method
$content = [regex]::Replace($content, $oldMethod, $newMethod, "Singleline")

Set-Content $file $content -NoNewline
