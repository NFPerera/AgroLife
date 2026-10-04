import json

from pipeline import __main__


def test_escribir_reemplaza_sin_dejar_temporales(tmp_path):
    carpeta = tmp_path / "pergamino"
    carpeta.mkdir()
    (carpeta / "region.json").write_text("viejo")
    __main__.escribir(carpeta, {"region.json": {"id": "pergamino", "nombre": "Pergamino"}, "lotes.json": {"lotes": []}})
    assert json.loads((carpeta / "region.json").read_text(encoding="utf-8"))["nombre"] == "Pergamino"
    assert (carpeta / "lotes.json").read_text(encoding="utf-8") == '{"lotes":[]}'
    assert not (tmp_path / "pergamino.tmp").exists()
