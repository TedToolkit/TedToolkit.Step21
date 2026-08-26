// Copyright (c) TedToolkit contributors.
// Licensed under the LGPL-3.0 license. See COPYING and COPYING.LESSER at the repository root.

#include <BRepPrimAPI_MakeBox.hxx>
#include <DESTEP_Parameters.hxx>
#include <IFSelect_ReturnStatus.hxx>
#include <STEPControl_Writer.hxx>

#include <iostream>

int main(int argc, char** argv)
{
  if (argc != 2)
  {
    std::cerr << "usage: occt_ap203_fixture <output.step>\n";
    return 64;
  }

  const TopoDS_Shape box = BRepPrimAPI_MakeBox(10.0, 20.0, 30.0).Shape();

  DESTEP_Parameters parameters;
  parameters.WriteSchema = DESTEP_Parameters::WriteMode_StepSchema_AP203;
  parameters.WriteProductName = "TedToolkit AP203 OCCT box 10x20x30 mm";
  parameters.WriteModelType = STEPControl_ManifoldSolidBrep;
  parameters.WriteSurfaceCurMode = false;
  parameters.WriteUnit = UnitsMethods_LengthUnit_Millimeter;
  parameters.WriteColor = false;
  parameters.WriteName = false;
  parameters.WriteLayer = false;
  parameters.WriteProps = false;
  parameters.WriteMetadata = false;
  parameters.WriteMaterial = false;

  STEPControl_Writer writer;
  const IFSelect_ReturnStatus transfer = writer.Transfer(
    box,
    STEPControl_ManifoldSolidBrep,
    parameters,
    true);
  if (transfer != IFSelect_RetDone)
  {
    std::cerr << "STEP transfer failed with status " << transfer << '\n';
    return 1;
  }

  const IFSelect_ReturnStatus write = writer.Write(argv[1]);
  if (write != IFSelect_RetDone)
  {
    std::cerr << "STEP write failed with status " << write << '\n';
    return 2;
  }

  return 0;
}
