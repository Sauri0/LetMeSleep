$ErrorActionPreference='Stop'
$repository=(Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$builder=Get-Content -LiteralPath (Join-Path $repository 'unity/Assets/LetMeSleep/Content/Editor/Environment/AlfaMapBuilder.cs') -Raw
$method=$builder.Substring($builder.IndexOf('internal static string ContentHash('))
$parts=$method -split 'if\(mapId=="house-patio-v1"\)',2
$filePattern='"((?:art_source|unity)/[^"\r\n]+\.(?:fbx|json|cs))"'
$files=@([regex]::Matches($parts[0],$filePattern)|ForEach-Object{$_.Groups[1].Value})
$houseFiles=@([regex]::Matches($parts[1],$filePattern)|ForEach-Object{$_.Groups[1].Value})
if($files.Count -ne 21 -or $houseFiles.Count -ne 2){throw 'Content hash file list must match builder'}
$sha=[System.Security.Cryptography.SHA256]::Create()
$hashes=@()
try{
    foreach($map in @('house-patio-v1','private-lobby-v1')){
        $payload=$map+"`n"
        $mapFiles=if($map -eq 'house-patio-v1'){@($files)+@($houseFiles)}else{@($files)}
        foreach($file in $mapFiles){$path=Join-Path $repository $file
            $bytes=if($file.EndsWith('.fbx')){[System.IO.File]::ReadAllBytes($path)}else{[System.Text.Encoding]::UTF8.GetBytes([System.IO.File]::ReadAllText($path).Replace("`r`n","`n").Replace("`r","`n"))}
            $hex=[System.BitConverter]::ToString($sha.ComputeHash([byte[]]$bytes)).Replace('-','').ToLowerInvariant()
            $payload+=$file+"`t"+$hex+"`n"
        }
        $hash='sha256-'+[System.BitConverter]::ToString($sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($payload))).Replace('-','').ToLowerInvariant()
        $hashes+=@{map_id=$map;content_hash=$hash;files=$mapFiles}
        Write-Output ($map+' '+$hash)
    }
}finally{$sha.Dispose()}
@{algorithm='SHA256 map ID and ordered file/hash records; UTF8 text newlines normalized toLF, FBX raw';files=$files;maps=$hashes} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $repository 'docs/unity/environment/ALFA-CONTENT-HASHES.json') -Encoding utf8
