"""Generate the pinned 10 x 20 x 30 mm OCCT AP242DIS interoperability fixture."""

from pathlib import Path
import re
import sys

from OCP.BRepPrimAPI import BRepPrimAPI_MakeBox
from OCP.IFSelect import IFSelect_RetDone
from OCP.Interface import Interface_Static
from OCP.STEPControl import STEPControl_Controller, STEPControl_ManifoldSolidBrep, STEPControl_Writer


def main() -> int:
    if len(sys.argv) != 2:
        raise SystemExit("usage: generate-ap242-fixture.py OUTPUT.step")

    output = Path(sys.argv[1]).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)

    STEPControl_Controller.Init_s()
    if not Interface_Static.SetCVal_s("write.step.schema", "AP242DIS"):
        raise RuntimeError("OCCT does not expose the AP242DIS writer schema")
    Interface_Static.SetCVal_s("write.step.unit", "MM")
    Interface_Static.SetCVal_s(
        "write.step.product.name", "TedToolkit AP242 OCCT box 10x20x30 mm"
    )
    Interface_Static.SetIVal_s("write.surfacecurve.mode", 0)
    Interface_Static.SetIVal_s("write.stepcaf.subshapes.name", 0)
    Interface_Static.SetIVal_s("write.stepcaf.color", 0)
    Interface_Static.SetIVal_s("write.stepcaf.layer", 0)
    Interface_Static.SetIVal_s("write.stepcaf.props", 0)

    shape = BRepPrimAPI_MakeBox(10.0, 20.0, 30.0).Shape()
    writer = STEPControl_Writer()
    if writer.Transfer(shape, STEPControl_ManifoldSolidBrep) != IFSelect_RetDone:
        raise RuntimeError("OCCT could not transfer the box to the AP242 model")
    if writer.Write(str(output)) != IFSelect_RetDone:
        raise RuntimeError("OCCT could not write the AP242 fixture")

    content = output.read_text(encoding="utf-8")
    canonical_file_name = (
        "FILE_NAME('occt-box-10x20x30-ap242.step','2026-09-06T00:00:00+08:00',"
        "('TedToolkit'),('TedToolkit'),'Open CASCADE STEP processor 7.9',"
        "'Open CASCADE 7.9','');\nFILE_SCHEMA"
    )
    content, replacements = re.subn(
        r"FILE_NAME\(.*?\);\s*FILE_SCHEMA",
        canonical_file_name,
        content,
        count=1,
        flags=re.DOTALL,
    )
    if replacements != 1:
        raise RuntimeError("OCCT output did not contain one FILE_NAME header")
    content = content.replace(
        "'ap242_managed_model_based_3d_engineering',2013",
        "'ap242_managed_model_based_3d_engineering_mim_lf',2013",
    )
    output.write_text(content.replace("\r\n", "\n"), encoding="utf-8", newline="\n")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
