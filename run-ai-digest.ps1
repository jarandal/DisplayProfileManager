$startDir = Get-Location

try {
    
    Set-Location $startDir
    ai-digest -i . -o _ai_codebase.md --ignore-file .aidigestignore --show-output-files sort 
    
   
    ai-digest -i C:\Users\jaran\AppData\Roaming\DisplayProfileManager -o $startDir\_ai_logs_y_profiles.md --ignore-file "$startDir\.aidigestignore" --show-output-files sort 

}
finally {
    Set-Location $startDir
}