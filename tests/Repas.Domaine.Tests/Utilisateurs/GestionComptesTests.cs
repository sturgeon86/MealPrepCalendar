using Repas.Domaine.Utilisateurs;

namespace Repas.Domaine.Tests.Utilisateurs;

public sealed class GestionComptesTests
{
    [Fact]
    public void OnPeutModifierLeCompteDUnAutre()
    {
        Assert.True(GestionComptes.PeutModifierActif(idDemandeur: 1, idCible: 2));
    }

    [Fact]
    public void OnNePeutPasModifierSonPropreCompte()
    {
        Assert.False(GestionComptes.PeutModifierActif(idDemandeur: 1, idCible: 1));
    }
}
