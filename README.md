# Sušenkov

**Sušenkov** je 3D low-poly adventura/platformer vytvořená v **Unity** pro **PC**.

Hráč se pohybuje po městě, sbírá roztroušené sušenky, plní úkoly pro místní obyvatele a za získané odměny si vylepšuje svou postavu. Město funguje na principu **3D Metroidvanie** – nová vylepšení zpřístupňují dříve nedostupné části mapy.

## Hra
Po útoku na továrny, kde se vyrábějí slavné Sušenkovské sušenky, se sušenky rozletěly po celém městě. Je na hráči, aby je všechny posbíral.

Hlavní herní smyčka:
**Prozkoumávání → sbírání sušenek → plnění úkolů → získávání odměn → prodej → upgrady → odemknutí nových oblastí**

### Hlavní mechaniky
* Plynulý pohyb a parkour
* Sbírání různých druhů sušenek
* Úkoly od NPC
* Ekonomika a zastavárna
* Upgrady postavy
* Kapacita batohu
* Vysavač na sušenky
* Denní a noční cyklus
* Dynamické počasí
* Náhodné eventy
* Objevování skrytých oblastí

## Sušenky

| Typ            | Hodnota | Vzácnost        | Výskyt                           |
| -------------- | ------: | --------------- | -------------------------------- |
| Obyčejná       |       1 | Běžná           | Ulice, chodníky, náměstí         |
| Zlatá          |       5 | Neobvyklá       | Střechy, lešení, skryté dvorky   |
| Magická        |      20 | Vzácná          | Interiéry, podzemí, odměny       |
| Rainbow Cookie |     100 | Extrémně vzácná | Skryté výzvy a speciální oblasti |

Hráč začíná s kapacitou batohu **20 sušenek**. Kapacitu lze postupně zvyšovat pomocí upgradů.

## Upgrady
Peníze získané ze zastavárny lze investovat do vylepšení postavy:

* **Rychlé ruce** – rychlejší sbírání sušenek
* **Větší ruce** – efektivnější sbírání
* **Batoh** – větší kapacita
* **Výdrž** – delší běh bez vyčerpání
* **Vysavač na sušenky** – sbírání sušenek podle směru pohledu
* **Vylepšení vysavače** – další rozšíření jeho schopností

## Svět
Město je rozdělené do několika oblastí, které postupně zpřístupňují nové mechaniky a výzvy:

* Okraj města
* Městský park
* Obchodní centrum
* Staveniště
* Podzemní metro
* Staré město
* Kanalizace
* Tovární obvod
* Centrum města
* Mrakodrap
* Epicentrum
* Finální tovární komplex

Nová vylepšení umožňují hráči dostat se do míst, která byla dříve nedostupná.

## Počasí a eventy
Svět reaguje na denní dobu a počasí.

### Počasí
* Den / noc
* Déšť
* Mlha
* Vítr

Některé sušenky a NPC jsou dostupní pouze za určitých podmínek.

### Náhodné eventy
* **Havárie dodávky** – na křižovatce se objeví hromada sušenek
* **Sušenkový déšť** – sušenky začnou padat z nebe
* **Pop-up obchodník** – objeví se obchodník se speciálními nabídkami

## Vizuální styl
Hra používá **low-poly 3D styl**.

Zvukový design obsahuje:

* zvukové efekty pohybu a sbírání
* ambientní zvuky města
* hudební doprovod
* speciální zvuky pro eventy a herní události

## Technologie
* **Engine:** Unity
* **Platforma:** PC
* **Rok:** 2026
* **Styl:** 3D Low Poly

## Tým
* **Štěpán Prokop**
* **Jiří Lhota**
* **Maxmilián Zálešák**

## Rozsah projektu
Plánovaný rozsah:

* **12** hlavních oblastí
* **30+** tajných místností
* **4** druhy sušenek
* **~30** NPC
* **20–30** vedlejších questů
* **20+** achievementů
* systém postupného vylepšování postavy

---

# Vývoj

## Požadavky

Pro vývoj projektu potřebujete:

* [Unity](https://unity.com/)
* Git
* Git LFS, pokud projekt obsahuje větší binární soubory

## Instalace projektu

1. Naklonujte repozitář:

   ```bash
   git clone https://github.com/stepprok/susenkov_game
   cd <REPOSITORY_FOLDER>
   ```

2. Pokud projekt používá Git LFS:

   ```bash
   git lfs install
   git lfs pull
   ```

3. Otevřete projekt v **Unity Hub**.

4. Vyberte projektovou složku a otevřete ji v odpovídající verzi Unity.

5. Po načtení projektu spusťte hlavní scénu a projekt můžete začít prozkoumávat.

## Struktura projektu

```text
Assets/
├── Art/
├── Audio/
├── Materials/
├── Prefabs/
├── Scenes/
├── Scripts/
├── UI/
└── ...
```

> Strukturu projektu je potřeba aktualizovat podle skutečného rozložení složek v repozitáři.

## Stav projektu

Projekt je aktivně ve vývoji. Některé systémy, oblasti a herní mechaniky jsou zatím ve fázi návrhu nebo implementace.
