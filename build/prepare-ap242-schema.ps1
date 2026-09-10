[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $SourcePath,

    [Parameter(Mandatory)]
    [string] $OutputPath,

    [Parameter(Mandatory)]
    [string] $ExpectedSourceHash,

    [string] $ExpectedOutputHash
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Sha256([string] $Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Assert-UniqueMarker([string] $Text, [string] $Marker) {
    $first = $Text.IndexOf($Marker, [StringComparison]::Ordinal)
    if ($first -lt 0 -or $first -ne $Text.LastIndexOf($Marker, [StringComparison]::Ordinal)) {
        throw "AP242 compatibility marker must occur exactly once: $Marker"
    }

    return $first
}

function Replace-Section(
    [string] $Text,
    [string] $StartMarker,
    [string] $EndMarker,
    [string] $Replacement
) {
    $start = Assert-UniqueMarker $Text $StartMarker
    $end = Assert-UniqueMarker $Text $EndMarker
    if ($end -le $start) {
        throw "AP242 compatibility section markers are out of order: $StartMarker"
    }

    return $Text.Substring(0, $start) + $Replacement + $Text.Substring($end)
}

$sourcePathFull = [IO.Path]::GetFullPath($SourcePath)
$outputPathFull = [IO.Path]::GetFullPath($OutputPath)
if (-not (Test-Path -LiteralPath $sourcePathFull -PathType Leaf)) {
    throw "Missing official AP242 EXPRESS source: $sourcePathFull"
}

$sourceHash = Get-Sha256 $sourcePathFull
if ($sourceHash -ne $ExpectedSourceHash) {
    throw "AP242 compatibility transform requires official source $ExpectedSourceHash, got ${sourceHash}: $sourcePathFull"
}

$source = [IO.File]::ReadAllText($sourcePathFull)
$datumProjection = @'
  the_datum                    : SET [1 : ?] OF datum := get_datums_for_datum_target(SELF);
'@
$source = Replace-Section `
    $source `
    '  the_datum                    : SET [1 : ?] OF datum :=' `
    '  combined_datum_target_string' `
    ($datumProjection + "`n")

$datumHelper = @'
FUNCTION get_datums_for_datum_target(input : datum_target) : SET OF datum;
LOCAL
  related : shape_aspect;
  result  : SET OF datum := [];
END_LOCAL;
  REPEAT i := 1 TO SIZEOF(input.target_basis_relationship);
    related := input.target_basis_relationship[i].related_shape_aspect;
    IF 'AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF.DATUM' IN TYPEOF(related)
    THEN
      result := result + related;
    END_IF;
  END_REPEAT;
  RETURN(result);
END_FUNCTION;

'@
$descendantFunction = 'FUNCTION get_descendant_occurrences'
$descendantIndex = Assert-UniqueMarker $source $descendantFunction
$source = $source.Insert($descendantIndex, $datumHelper)

$csgFunctions = @'
FUNCTION valid_csg_2d_operand(input : boolean_operand_2d) : BOOLEAN;
  CASE TRUE OF
    ('AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF.PRIMITIVE_2D' IN
     TYPEOF(input)) : BEGIN
      IF SIZEOF(['AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF.CIRCULAR_AREA',
                 'AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF.COMPLEX_AREA',
                 'AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF.ELLIPTIC_AREA',
                 'AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF.HALF_SPACE_2D',
                 'AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF.POLYGONAL_AREA',
                 'AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF.PRIMITIVE_2D_WITH_INNER_BOUNDARY',
                 'AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF.RECTANGULAR_AREA'] * TYPEOF(input)) > 0
      THEN
        RETURN(TRUE);
      ELSE
        RETURN(FALSE);
      END_IF;
    END;
    ('AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF.BOOLEAN_RESULT_2D' IN
     TYPEOF(input)) : RETURN(valid_csg_2d_operand(input\boolean_result_2d.first_operand) AND
                             valid_csg_2d_operand(input\boolean_result_2d.second_operand));
    OTHERWISE: RETURN(FALSE);
  END_CASE;
END_FUNCTION;

FUNCTION valid_csg_2d_primitives(input : csg_solid_2d) : BOOLEAN;
  RETURN(valid_csg_2d_operand(input\csg_solid_2d.tree_root_expression));
END_FUNCTION;

'@
$source = Replace-Section `
    $source `
    'FUNCTION valid_csg_2d_primitives' `
    'FUNCTION valid_datum_target_parameters' `
    $csgFunctions

$outputDirectory = Split-Path -Parent $outputPathFull
[IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$candidatePath = $outputPathFull + '.' + [Guid]::NewGuid().ToString('N') + '.candidate'
try {
    [IO.File]::WriteAllText($candidatePath, $source, [Text.UTF8Encoding]::new($false))
    $outputHash = Get-Sha256 $candidatePath
    if (-not [string]::IsNullOrWhiteSpace($ExpectedOutputHash) -and $outputHash -ne $ExpectedOutputHash) {
        throw "AP242 compatibility output failed SHA-256 verification. Expected $ExpectedOutputHash, got $outputHash"
    }

    [IO.File]::Move($candidatePath, $outputPathFull, $true)
    Write-Output "Prepared AP242 generation input: $outputHash"
}
finally {
    Remove-Item -LiteralPath $candidatePath -Force -ErrorAction SilentlyContinue
}
