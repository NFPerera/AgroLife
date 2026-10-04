import pytest

from pipeline import config, descargas


def test_obtener_no_repite_la_descarga(tmp_path, monkeypatch):
    llamadas = []

    class Resp:
        status_code = 200
        def raise_for_status(self): pass
        def iter_content(self, n): yield b"hola"
        def __enter__(self): return self
        def __exit__(self, *a): pass

    monkeypatch.setattr(descargas.requests, "get", lambda *a, **k: (llamadas.append(1), Resp())[1])
    destino = tmp_path / "x" / "a.bin"
    descargas.obtener("https://ejemplo/a", destino)
    descargas.obtener("https://ejemplo/a", destino)
    assert destino.read_bytes() == b"hola" and len(llamadas) == 1 and not destino.with_suffix(".bin.part").exists()


def test_oni_parsea_columnas(tmp_path, monkeypatch):
    texto = " SEAS  YR   TOTAL   ANOM\n  DJF 1950  25.01  -1.32\n  NDJ 2015  29.30   2.64\n"
    archivo = tmp_path / "oni.ascii.txt"
    archivo.write_text(texto)
    monkeypatch.setattr(descargas, "obtener", lambda url, destino, **k: archivo)
    df = descargas.oni()
    assert list(df.columns) == ["seas", "anio", "anom"] and df.iloc[1].tolist() == ["NDJ", 2015, 2.64]


def test_reemplazo_atomico_no_deja_nada_si_falla(tmp_path):
    destino = tmp_path / "capa.gpkg"
    def escribir_a_medias(p):
        p.write_text("a medias")
        raise OSError("corte")
    with pytest.raises(OSError):
        descargas.reemplazar_atomico(destino, escribir_a_medias)
    assert not destino.exists()
    descargas.reemplazar_atomico(destino, lambda p: p.write_text("completo"))
    assert destino.read_text() == "completo" and list(tmp_path.iterdir()) == [destino]


def test_cache_de_soilgrids_depende_del_bbox(tmp_path, monkeypatch):
    monkeypatch.setattr(config, "CACHE", tmp_path)
    class Resp:
        headers = {"content-type": "image/tiff"}
        content = b"II*"
        def raise_for_status(self): pass
    monkeypatch.setattr(descargas.requests, "get", lambda *a, **k: Resp())
    a = descargas.soilgrids("sand", "0-5cm", (-60.96, -34.2, -60.12, -33.52))
    b = descargas.soilgrids("sand", "0-5cm", (-60.9642, -34.1966, -60.1188, -33.5202))
    assert a != b and a.exists() and b.exists()
